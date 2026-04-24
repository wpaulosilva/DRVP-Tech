using DanceSchoolApp.Server.Data;
using DanceSchoolApp.Server.DTOs;
using DanceSchoolApp.Server.DTOs.Classes;
using DanceSchoolApp.Server.DTOs.Coach;
using DanceSchoolApp.Server.DTOs.Staff;
using DanceSchoolApp.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace DanceSchoolApp.Server.Services
{
    public class CoachPortalService
    {
        private readonly AppDbContext _context;

        public CoachPortalService(AppDbContext context)
        {
            _context = context;
        }

        // ─── Resolve coach ────────────────────────────────────────────────────

        /// <summary>
        /// Returns the Coach for the given userId, or throws
        /// UnauthorizedAccessException if no Coach row exists.
        /// </summary>
        public async Task<Coach> ResolveCoachAsync(int userId)
        {
            var coach = await _context.Coaches
                .Include(c => c.CoachNavigation)
                    .ThenInclude(u => u.PersonInfo)
                .FirstOrDefaultAsync(c => c.CoachId == userId);

            if (coach is null)
                throw new UnauthorizedAccessException(
                    "No coach profile found for the authenticated user.");

            return coach;
        }

        // ─── Dashboard ────────────────────────────────────────────────────────

        public async Task<CoachDashboardResponse> GetDashboardAsync(int coachId)
        {
            var now       = DateTime.UtcNow;
            int thisYear  = now.Year;
            int thisMonth = now.Month;

            var coach = await _context.Coaches
                .Include(c => c.CoachNavigation)
                    .ThenInclude(u => u.PersonInfo)
                .FirstAsync(c => c.CoachId == coachId);

            var coachName = ResolveCoachName(coach);

            var classesTaughtThisMonth = await _context.CoachClasses
                .CountAsync(c =>
                    c.IdCoach == coachId &&
                    c.Status == (byte)CoachClassStatus.Validated &&
                    c.StartDatetime.Year  == thisYear &&
                    c.StartDatetime.Month == thisMonth);

            var classesUpcoming = await _context.CoachClasses
                .CountAsync(c =>
                    c.IdCoach == coachId &&
                    (c.Status == (byte)CoachClassStatus.StaffApproved ||
                     c.Status == (byte)CoachClassStatus.Approved) &&
                    c.StartDatetime > now);

            var validationsPending = await _context.CoachClasses
                .CountAsync(c =>
                    c.IdCoach == coachId &&
                    c.Status == (byte)CoachClassStatus.Finished &&
                    c.CoachValidationStatus == (byte)CoachValidationStatus.Pending);

            var upcomingClasses = await _context.CoachClasses
                .Include(c => c.IdModalityNavigation)
                .Include(c => c.IdStudioNavigation)
                .Include(c => c.Participants)
                    .ThenInclude(p => p.IdStudentNavigation)
                        .ThenInclude(s => s.PersonInfo)
                .Where(c =>
                    c.IdCoach == coachId &&
                    (c.Status == (byte)CoachClassStatus.StaffApproved ||
                     c.Status == (byte)CoachClassStatus.Approved) &&
                    c.StartDatetime > now)
                .OrderBy(c => c.StartDatetime)
                .Take(4)
                .ToListAsync();

            return new CoachDashboardResponse
            {
                CoachName              = coachName,
                ClassesTaughtThisMonth = classesTaughtThisMonth,
                ClassesUpcoming        = classesUpcoming,
                ValidationsPending     = validationsPending,
                UpcomingClasses        = upcomingClasses.Select(c => new CoachUpcomingClass
                {
                    ClassId       = c.ClassId,
                    StartDatetime = c.StartDatetime,
                    EndDatetime   = c.EndDatetime,
                    ModalityName  = c.IdModalityNavigation.Name,
                    StudioName    = c.IdStudioNavigation.Name,
                    StudentNames  = c.Participants
                        .Select(p => ResolveStudentName(p.IdStudentNavigation))
                        .ToList(),
                    Status        = (CoachClassStatus)c.Status
                }).ToList()
            };
        }

        // ─── Agenda ───────────────────────────────────────────────────────────

        public async Task<List<AgendaClassItem>> GetAgendaAsync(
            int coachId, DateOnly from, DateOnly to)
        {
            var fromDt = from.ToDateTime(TimeOnly.MinValue);
            var toDt   = to.ToDateTime(new TimeOnly(23, 59, 59, 999));

            var classes = await _context.CoachClasses
                .Include(c => c.IdModalityNavigation)
                .Include(c => c.IdStudioNavigation)
                .Include(c => c.IdCoachNavigation)
                    .ThenInclude(coach => coach.CoachNavigation)
                        .ThenInclude(u => u.PersonInfo)
                .Include(c => c.Participants)
                    .ThenInclude(p => p.IdStudentNavigation)
                        .ThenInclude(s => s.PersonInfo)
                .Where(c =>
                    c.IdCoach == coachId &&
                    c.StartDatetime >= fromDt &&
                    c.StartDatetime <= toDt)
                .OrderBy(c => c.StartDatetime)
                .ToListAsync();

            return classes.Select(c => new AgendaClassItem
            {
                ClassId             = c.ClassId,
                StartDatetime       = c.StartDatetime,
                EndDatetime         = c.EndDatetime,
                ModalityName        = c.IdModalityNavigation.Name,
                StudioName          = c.IdStudioNavigation.Name,
                CoachName           = ResolveCoachName(c.IdCoachNavigation),
                StudentNames        = c.Participants
                    .Select(p => ResolveStudentName(p.IdStudentNavigation))
                    .ToList(),
                Status              = (CoachClassStatus)c.Status,
                CurrentParticipants = c.Participants.Count,
                MaxParticipants     = c.MaxParticipants
            }).ToList();
        }

        // ─── Validate — requests tab ──────────────────────────────────────────

        public async Task<PagedResult<CoachPendingRequestItem>> GetPendingRequestsAsync(
            int coachId, int page, int pageSize)
        {
            var dbQuery = _context.CoachClasses
                .Include(c => c.IdModalityNavigation)
                .Include(c => c.IdStudioNavigation)
                .Include(c => c.CreatedByNavigation)
                    .ThenInclude(u => u.PersonInfo)
                .Include(c => c.Participants)
                    .ThenInclude(p => p.IdStudentNavigation)
                        .ThenInclude(s => s.PersonInfo)
                .Where(c =>
                    c.IdCoach == coachId &&
                    c.Status == (byte)CoachClassStatus.StaffApproved);

            var total = await dbQuery.CountAsync();

            var classes = await dbQuery
                .OrderByDescending(c => c.StartDatetime)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var items = classes.Select(c => new CoachPendingRequestItem
            {
                ClassId           = c.ClassId,
                RequestedAt       = c.CreatedAt,
                StudentNames      = c.Participants
                    .Select(p => ResolveStudentName(p.IdStudentNavigation))
                    .ToList(),
                ParentName        = ResolveUserName(c.CreatedByNavigation),
                PreferredDatetime = c.StartDatetime,
                EndDatetime       = c.EndDatetime,
                DurationMinutes   = (int)(c.EndDatetime - c.StartDatetime).TotalMinutes,
                MaxParticipants   = c.MaxParticipants,
                StudioName        = c.IdStudioNavigation.Name,
                ModalityName      = c.IdModalityNavigation.Name
            }).ToList();

            return new PagedResult<CoachPendingRequestItem>
            {
                Items      = items,
                TotalCount = total,
                Page       = page,
                PageSize   = pageSize
            };
        }

        // ─── Validate — validations tab ───────────────────────────────────────

        public async Task<PagedResult<CoachValidationItem>> GetPendingValidationsAsync(
            int coachId, int page, int pageSize)
        {
            var dbQuery = _context.CoachClasses
                .Include(c => c.IdModalityNavigation)
                .Include(c => c.IdStudioNavigation)
                .Include(c => c.Participants)
                    .ThenInclude(p => p.IdStudentNavigation)
                        .ThenInclude(s => s.PersonInfo)
                .Where(c =>
                    c.IdCoach == coachId &&
                    c.CoachValidationStatus == 0 &&
                    c.Status == (byte)CoachClassStatus.Finished);

            var total = await dbQuery.CountAsync();

            var classes = await dbQuery
                .OrderByDescending(c => c.StartDatetime)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var items = classes.Select(c => new CoachValidationItem
            {
                ClassId               = c.ClassId,
                Participants          = c.Participants.Select(p => new ParticipantSummaryItem
                {
                    ParticipantId    = p.ParticipantId,
                    StudentName      = ResolveStudentName(p.IdStudentNavigation),
                    ValidationStatus = p.ValidationStatus
                }).ToList(),
                StartDatetime         = c.StartDatetime,
                EndDatetime           = c.EndDatetime,
                StudioName            = c.IdStudioNavigation.Name,
                MaxParticipants       = c.MaxParticipants,
                ModalityName          = c.IdModalityNavigation.Name,
                ExpiresAt             = c.StartDatetime.AddHours(48),
                CoachValidationStatus = c.CoachValidationStatus
            }).ToList();

            return new PagedResult<CoachValidationItem>
            {
                Items      = items,
                TotalCount = total,
                Page       = page,
                PageSize   = pageSize
            };
        }

        // ─── Private helpers ──────────────────────────────────────────────────

        private static string ResolveCoachName(Coach coach)
        {
            var p = coach.CoachNavigation?.PersonInfo;
            return p is not null
                ? $"{p.FirstName} {p.LastName}".Trim()
                : coach.CoachNavigation?.Username ?? $"Coach {coach.CoachId}";
        }

        private static string ResolveStudentName(Student student)
        {
            var p = student.PersonInfo;
            return p is not null
                ? $"{p.FirstName} {p.LastName}".Trim()
                : $"Student {student.StudentId}";
        }

        private static string ResolveUserName(User user)
        {
            var p = user.PersonInfo;
            return p is not null
                ? $"{p.FirstName} {p.LastName}".Trim()
                : user.Username;
        }
    }
}
