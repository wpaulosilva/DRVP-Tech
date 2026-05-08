using DanceSchoolApp.Server.Data;
using DanceSchoolApp.Server.DTOs;
using DanceSchoolApp.Server.DTOs.Classes;
using DanceSchoolApp.Server.DTOs.Staff;
using DanceSchoolApp.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace DanceSchoolApp.Server.Services
{
    public class StaffDashboardService
    {
        private readonly AppDbContext _context;

        public StaffDashboardService(AppDbContext context)
        {
            _context = context;
        }

        //  Dashboard 

        public async Task<StaffDashboardResponse> GetDashboardAsync()
        {
            var now       = DateTime.Now;
            int thisYear  = now.Year;
            int thisMonth = now.Month;

            var totalParents = await _context.Users
                .CountAsync(u => u.IsActive &&
                                 u.IdRoles.Any(r => r.RoleId == Roles.Parent));

            var totalCoaches = await _context.Users
                .CountAsync(u => u.IsActive &&
                                 u.IdRoles.Any(r => r.RoleId == Roles.Coach));

            var totalActiveStudents = await _context.Students
                .CountAsync(s => s.IsActive && s.AcceptanceStatus == 1);

            var totalActiveEvents = await _context.Events
                .CountAsync(e => e.IsActive);

            var classesScheduledThisMonth = await _context.CoachClasses
                .CountAsync(c =>
                    c.StartDatetime.Year  == thisYear  &&
                    c.StartDatetime.Month == thisMonth &&
                    c.Status != (byte)CoachClassStatus.Rejected  &&
                    c.Status != (byte)CoachClassStatus.Cancelled);

            var classesCompletedThisMonth = await _context.CoachClasses
                .CountAsync(c =>
                    c.StartDatetime.Year  == thisYear  &&
                    c.StartDatetime.Month == thisMonth &&
                    c.Status == (byte)CoachClassStatus.Validated);

            var classesPendingValidation = await _context.CoachClasses
                .CountAsync(c => c.Status == (byte)CoachClassStatus.Pending);

            var upcomingEvents = await _context.Events
                .Where(e => e.IsActive && e.StartDatetime >= now)
                .OrderBy(e => e.StartDatetime)
                .Take(3)
                .Select(e => new EventSummary
                {
                    EventId       = e.EventId,
                    Title         = e.Title,
                    StartDatetime = e.StartDatetime,
                    EndDatetime   = e.EndDatetime
                })
                .ToListAsync();

            return new StaffDashboardResponse
            {
                TotalParents                 = totalParents,
                TotalCoaches                 = totalCoaches,
                TotalActiveStudents          = totalActiveStudents,
                TotalActiveEvents            = totalActiveEvents,
                ClassesScheduledThisMonth    = classesScheduledThisMonth,
                ClassesCompletedThisMonth    = classesCompletedThisMonth,
                ClassesPendingValidation     = classesPendingValidation,
                UpcomingEvents               = upcomingEvents
            };
        }

        //  Agenda 

        public async Task<List<AgendaClassItem>> GetAgendaAsync(
            DateOnly from, DateOnly to, int? studioId, byte? status)
        {
            var fromDt = from.ToDateTime(TimeOnly.MinValue);
            var toDt   = to.ToDateTime(new TimeOnly(23, 59, 59, 999));

            var query = _context.CoachClasses
                .Include(c => c.IdModalityNavigation)
                .Include(c => c.IdStudioNavigation)
                .Include(c => c.IdCoachNavigation)
                    .ThenInclude(coach => coach.CoachNavigation)
                        .ThenInclude(u => u.PersonInfo)
                .Include(c => c.Participants)
                    .ThenInclude(p => p.IdStudentNavigation)
                        .ThenInclude(s => s.PersonInfo)
                .Where(c => c.StartDatetime >= fromDt && c.StartDatetime <= toDt);

            if (studioId.HasValue)
                query = query.Where(c => c.IdStudio == studioId.Value);

            if (status.HasValue)
                query = query.Where(c => c.Status == status.Value);

            var classes = await query
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

        //  Validate classes 

        public async Task<PagedResult<ValidateClassItem>> GetValidateClassesAsync(
            string tab, int page, int pageSize)
        {
            byte statusByte = tab.ToLowerInvariant() switch
            {
                "finished" => (byte)CoachClassStatus.Finished,
                "pending"  => (byte)CoachClassStatus.Pending,
                _          => (byte)CoachClassStatus.Requested
            };

            var dbQuery = _context.CoachClasses
                .Include(c => c.IdModalityNavigation)
                .Include(c => c.IdStudioNavigation)
                .Include(c => c.IdCoachNavigation)
                    .ThenInclude(coach => coach.CoachNavigation)
                        .ThenInclude(u => u.PersonInfo)
                .Include(c => c.CreatedByNavigation)
                    .ThenInclude(u => u.PersonInfo)
                .Include(c => c.Participants)
                    .ThenInclude(p => p.IdStudentNavigation)
                        .ThenInclude(s => s.PersonInfo)
                .Include(c => c.Participants)
                    .ThenInclude(p => p.IdStudentNavigation)
                        .ThenInclude(s => s.ParentUser)
                            .ThenInclude(u => u.PersonInfo)
                .Where(c => c.Status == statusByte);

            var total = await dbQuery.CountAsync();

            var classes = await dbQuery
                .OrderByDescending(c => c.StartDatetime)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var items = classes.Select(c =>
            {
                var parts = c.Participants.ToList();
                return new ValidateClassItem
                {
                    ClassId               = c.ClassId,
                    ModalityName          = c.IdModalityNavigation.Name,
                    StudioName            = c.IdStudioNavigation?.Name,
                    StartDatetime         = c.StartDatetime,
                    EndDatetime           = c.EndDatetime,
                    MaxParticipants       = c.MaxParticipants,
                    DurationMinutes       = (int)(c.EndDatetime - c.StartDatetime).TotalMinutes,
                    CoachName             = ResolveCoachName(c.IdCoachNavigation),
                    ParentName            = ResolveUserName(c.CreatedByNavigation),
                    CoachValidationStatus = c.CoachValidationStatus,
                    ParticipantSummary    = new ParticipantSummaryDto
                    {
                        Total     = parts.Count,
                        Confirmed = parts.Count(p => p.ValidationStatus == 1),
                        Disputed  = parts.Count(p => p.ValidationStatus == 2),
                        Pending   = parts.Count(p => p.ValidationStatus == 0)
                    },
                    Participants          = parts.Select(p => new ParticipantSummaryItem
                    {
                        ParticipantId    = p.ParticipantId,
                        StudentName      = ResolveStudentName(p.IdStudentNavigation),
                        ValidationStatus = p.ValidationStatus,
                        ParentName       = p.IdStudentNavigation.ParentUser is not null
                            ? ResolveUserName(p.IdStudentNavigation.ParentUser)
                            : null
                    }).ToList()
                };
            }).ToList();

            return new PagedResult<ValidateClassItem>
            {
                Items      = items,
                TotalCount = total,
                Page       = page,
                PageSize   = pageSize
            };
        }

        //  Validate students 

        public async Task<PagedResult<ValidateStudentItem>> GetValidateStudentsAsync(
            string status, int page, int pageSize)
        {
            var query = _context.Students
                .Include(s => s.PersonInfo)
                .Include(s => s.ParentUser)
                    .ThenInclude(u => u.PersonInfo)
                .AsQueryable();

            query = status.ToLowerInvariant() switch
            {
                "pending"  => query.Where(s => s.AcceptanceStatus == 0),
                "approved" => query.Where(s => s.AcceptanceStatus == 1),
                _          => query   // "all" — no filter
            };

            var total = await query.CountAsync();

            var students = await query
                .OrderBy(s => s.StudentId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var items = students.Select(s => new ValidateStudentItem
            {
                StudentId        = s.StudentId,
                FirstName        = s.PersonInfo?.FirstName,
                LastName         = s.PersonInfo?.LastName,
                BirthDate        = s.PersonInfo?.BirthDate,
                Phone            = s.PersonInfo?.Phone,
                Address          = s.PersonInfo?.Address,
                Nif              = MaskNif(s.PersonInfo?.Nif),
                AcceptanceStatus = s.AcceptanceStatus,
                SubmittedAt      = DateOnly.MinValue, // TODO: Student has no created_at column
                ParentName       = ResolveUserName(s.ParentUser),
                ParentEmail      = s.ParentUser?.Email
            }).ToList();

            return new PagedResult<ValidateStudentItem>
            {
                Items      = items,
                TotalCount = total,
                Page       = page,
                PageSize   = pageSize
            };
        }

        //  Private helpers 

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

        private static string? ResolveUserName(User? user)
        {
            if (user is null) return null;
            var p = user.PersonInfo;
            return p is not null
                ? $"{p.FirstName} {p.LastName}".Trim()
                : user.Username;
        }

        // Returns null for null input; otherwise "•••••••" + last 2 characters.
        private static string? MaskNif(string? nif)
        {
            if (nif is null) return null;
            if (nif.Length < 2) return "•••••••••";
            return "•••••••" + nif.Substring(nif.Length - 2);
        }
    }
}
