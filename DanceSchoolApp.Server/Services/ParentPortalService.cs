using DanceSchoolApp.Server.Data;
using DanceSchoolApp.Server.DTOs;
using DanceSchoolApp.Server.DTOs.Classes;
using DanceSchoolApp.Server.DTOs.Parent;
using DanceSchoolApp.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace DanceSchoolApp.Server.Services
{
    public class ParentPortalService
    {
        private readonly AppDbContext _context;

        public ParentPortalService(AppDbContext context)
        {
            _context = context;
        }

        // ─── Dashboard ────────────────────────────────────────────────────────

        public async Task<ParentDashboardResponse> GetDashboardAsync(int userId)
        {
            var now = DateTime.UtcNow;

            var user = await _context.Users
                .Include(u => u.PersonInfo)
                .FirstOrDefaultAsync(u => u.UserId == userId);

            var parentName = ResolveUserName(user!);

            var totalScheduled = await _context.CoachClasses
                .CountAsync(c =>
                    c.CreatedBy == userId &&
                    c.Status != (byte)CoachClassStatus.Rejected &&
                    c.Status != (byte)CoachClassStatus.Cancelled);

            var awaitingValidation = await _context.Participants
                .CountAsync(p =>
                    p.IdStudentNavigation.ParentUserId == userId &&
                    p.ValidationStatus == 0 &&
                    p.IdCoachClassNavigation.Status == (byte)CoachClassStatus.Finished);

            var upcomingClasses = await _context.CoachClasses
                .Include(c => c.IdModalityNavigation)
                .Include(c => c.IdStudioNavigation)
                .Include(c => c.IdCoachNavigation)
                    .ThenInclude(coach => coach.CoachNavigation)
                        .ThenInclude(u => u.PersonInfo)
                .Include(c => c.Participants)
                    .ThenInclude(p => p.IdStudentNavigation)
                        .ThenInclude(s => s.PersonInfo)
                .Where(c =>
                    c.CreatedBy == userId &&
                    c.Status == (byte)CoachClassStatus.Approved &&
                    c.StartDatetime > now)
                .OrderBy(c => c.StartDatetime)
                .Take(4)
                .ToListAsync();

            return new ParentDashboardResponse
            {
                ParentName                = parentName,
                TotalScheduledClasses     = totalScheduled,
                ClassesAwaitingValidation = awaitingValidation,
                UpcomingClasses           = upcomingClasses.Select(MapToUpcomingClass).ToList()
            };
        }

        // ─── My classes (calendar) ────────────────────────────────────────────

        public async Task<List<ParentUpcomingClass>> GetMyClassesAsync(
            int userId, DateOnly from, DateOnly to)
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
                    c.CreatedBy == userId &&
                    c.StartDatetime >= fromDt &&
                    c.StartDatetime <= toDt)
                .OrderBy(c => c.StartDatetime)
                .ToListAsync();

            return classes.Select(MapToUpcomingClass).ToList();
        }

        // ─── Open classes ─────────────────────────────────────────────────────

        public async Task<PagedResult<OpenClassItem>> GetOpenClassesAsync(
            int? modalityId, int page, int pageSize)
        {
            var now = DateTime.UtcNow;

            var baseQuery = _context.CoachClasses
                .Include(c => c.IdModalityNavigation)
                .Include(c => c.IdStudioNavigation)
                .Include(c => c.IdCoachNavigation)
                    .ThenInclude(coach => coach.CoachNavigation)
                        .ThenInclude(u => u.PersonInfo)
                .Include(c => c.Participants)
                .Where(c =>
                    c.Status == (byte)CoachClassStatus.Approved &&
                    c.StartDatetime > now &&
                    c.Participants.Count < c.MaxParticipants);

            if (modalityId.HasValue)
                baseQuery = baseQuery.Where(c => c.IdModality == modalityId.Value);

            var total = await baseQuery.CountAsync();

            var classes = await baseQuery
                .OrderBy(c => c.StartDatetime)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var items = classes.Select(c =>
            {
                int current = c.Participants.Count;
                return new OpenClassItem
                {
                    ClassId             = c.ClassId,
                    StartDatetime       = c.StartDatetime,
                    EndDatetime         = c.EndDatetime,
                    DurationMinutes     = (int)(c.EndDatetime - c.StartDatetime).TotalMinutes,
                    ModalityName        = c.IdModalityNavigation.Name,
                    CoachName           = ResolveCoachName(c.IdCoachNavigation),
                    StudioName          = c.IdStudioNavigation.Name,
                    CurrentParticipants = current,
                    MaxParticipants     = c.MaxParticipants,
                    SpotsAvailable      = c.MaxParticipants - current
                };
            }).ToList();

            return new PagedResult<OpenClassItem>
            {
                Items      = items,
                TotalCount = total,
                Page       = page,
                PageSize   = pageSize
            };
        }

        // ─── Pending validations ──────────────────────────────────────────────

        // Returns one entry per CLASS (not per participant).
        // Only shows classes that have at least one of this parent's students
        // still awaiting validation. The Participants list contains all of
        // this parent's enrolled students — including those already responded.
        public async Task<PagedResult<ParentValidateItem>> GetPendingValidationsAsync(
            int userId, int page, int pageSize)
        {
            var dbQuery = _context.CoachClasses
                .Include(c => c.IdModalityNavigation)
                .Include(c => c.IdCoachNavigation)
                    .ThenInclude(coach => coach.CoachNavigation)
                        .ThenInclude(u => u.PersonInfo)
                .Include(c => c.CreatedByNavigation)
                    .ThenInclude(u => u.PersonInfo)
                .Include(c => c.Participants)
                    .ThenInclude(p => p.IdStudentNavigation)
                        .ThenInclude(s => s.PersonInfo)
                .Where(c =>
                    c.CoachValidatedAt != null &&
                    (c.Status == (byte)CoachClassStatus.Finished ||
                     c.Status == (byte)CoachClassStatus.Pending) &&
                    c.Participants.Any(p =>
                        p.IdStudentNavigation.ParentUserId == userId &&
                        p.ValidationStatus == 0));

            var total = await dbQuery.CountAsync();

            var classes = await dbQuery
                .OrderBy(c => c.StartDatetime)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var items = classes.Select(c =>
            {
                // Include only this parent's students in the participants list
                var myParticipants = c.Participants
                    .Where(p => p.IdStudentNavigation.ParentUserId == userId)
                    .ToList();

                return new ParentValidateItem
                {
                    ClassId       = c.ClassId,
                    ModalityName  = c.IdModalityNavigation.Name,
                    StartDatetime = c.StartDatetime,
                    EndDatetime   = c.EndDatetime,
                    CoachName     = ResolveCoachName(c.IdCoachNavigation),
                    ExpiresAt     = c.StartDatetime.AddHours(48),
                    MaxParticipants = c.MaxParticipants,
                    CreatedByName = ResolveUserName(c.CreatedByNavigation),
                    Participants  = myParticipants.Select(p => new ParticipantSummaryItem
                    {
                        ParticipantId    = p.ParticipantId,
                        StudentName      = ResolveStudentName(p.IdStudentNavigation),
                        ValidationStatus = p.ValidationStatus
                    }).ToList()
                };
            }).ToList();

            return new PagedResult<ParentValidateItem>
            {
                Items      = items,
                TotalCount = total,
                Page       = page,
                PageSize   = pageSize
            };
        }

        // ─── My students ──────────────────────────────────────────────────────

        public async Task<List<ParentStudentItem>> GetMyStudentsAsync(int userId)
        {
            var students = await _context.Students
                .Include(s => s.PersonInfo)
                .Where(s => s.ParentUserId == userId && s.IsActive)
                .ToListAsync();

            return students.Select(s => new ParentStudentItem
            {
                StudentId        = s.StudentId,
                FirstName        = s.PersonInfo?.FirstName,
                LastName         = s.PersonInfo?.LastName,
                Age              = ComputeAge(s.PersonInfo?.BirthDate),
                BirthDate        = s.PersonInfo?.BirthDate,
                Phone            = s.PersonInfo?.Phone,
                Address          = s.PersonInfo?.Address,
                Nif              = s.PersonInfo?.Nif,
                AcceptanceStatus = s.AcceptanceStatus,
                IsActive         = s.IsActive
            }).ToList();
        }

        // ─── School inventory ─────────────────────────────────────────────────

        public async Task<PagedResult<InventoryItemCard>> GetSchoolInventoryAsync(
            int? categoryId, string? search, int page, int pageSize)
        {
            var baseQuery = _context.Items
                .Include(i => i.ItemVariants)
                .Include(i => i.ItemImages)
                .Include(i => i.IdCategoryNavigation)
                .Where(i =>
                    i.IsActive &&
                    i.FromSchool &&
                    i.ItemVariants.Any(v => v.IsActive == true && v.Quantity > 0));

            if (categoryId.HasValue)
                baseQuery = baseQuery.Where(i => i.IdCategory == categoryId.Value);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                baseQuery = baseQuery.Where(i => i.Name.ToLower().Contains(term));
            }

            var total = await baseQuery.CountAsync();

            var items = await baseQuery
                .OrderBy(i => i.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var mapped = items.Select(i =>
            {
                var activeVariants = i.ItemVariants
                    .Where(v => v.IsActive == true && v.Quantity > 0)
                    .ToList();

                return new InventoryItemCard
                {
                    ItemId        = i.ItemId,
                    Name          = i.Name,
                    Description   = i.Description,
                    CategoryName  = i.IdCategoryNavigation?.CatgName,
                    ImageUrl      = i.ItemImages.Select(img => img.ImageUrl).FirstOrDefault(),
                    LowestPrice   = activeVariants.Any(v => v.Price.HasValue)
                        ? activeVariants.Where(v => v.Price.HasValue).Min(v => v.Price)
                        : null,
                    VariantCount  = activeVariants.Count
                };
            }).ToList();

            return new PagedResult<InventoryItemCard>
            {
                Items      = mapped,
                TotalCount = total,
                Page       = page,
                PageSize   = pageSize
            };
        }

        // ─── Community inventory ──────────────────────────────────────────────

        public async Task<PagedResult<CommunityItemCard>> GetCommunityInventoryAsync(
            int? categoryId, decimal? maxPrice, string? search, int page, int pageSize)
        {
            var baseQuery = _context.Items
                .Include(i => i.ItemVariants)
                .Include(i => i.ItemImages)
                .Include(i => i.IdCategoryNavigation)
                .Include(i => i.IdOwnerNavigation)
                    .ThenInclude(u => u!.PersonInfo)
                .Where(i => i.IsActive && !i.FromSchool);

            if (categoryId.HasValue)
                baseQuery = baseQuery.Where(i => i.IdCategory == categoryId.Value);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                baseQuery = baseQuery.Where(i => i.Name.ToLower().Contains(term));
            }

            if (maxPrice.HasValue)
            {
                // Keep items where at least one variant has price <= maxPrice
                baseQuery = baseQuery.Where(i =>
                    i.ItemVariants.Any(v => v.Price.HasValue && v.Price <= maxPrice.Value));
            }

            var total = await baseQuery.CountAsync();

            var items = await baseQuery
                .OrderBy(i => i.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var mapped = items.Select(i =>
            {
                decimal? lowestPrice = i.ItemVariants.Any(v => v.Price.HasValue)
                    ? i.ItemVariants.Where(v => v.Price.HasValue).Min(v => v.Price)
                    : null;

                return new CommunityItemCard
                {
                    ItemId        = i.ItemId,
                    Name          = i.Name,
                    Description   = i.Description,
                    CategoryName  = i.IdCategoryNavigation?.CatgName,
                    ImageUrl      = i.ItemImages.Select(img => img.ImageUrl).FirstOrDefault(),
                    LowestPrice   = lowestPrice,
                    ContactPhone  = i.ContactPhone,
                    ContactEmail  = i.ContactEmail,
                    OwnerName     = ResolveUserName(i.IdOwnerNavigation)
                };
            }).ToList();

            return new PagedResult<CommunityItemCard>
            {
                Items      = mapped,
                TotalCount = total,
                Page       = page,
                PageSize   = pageSize
            };
        }

        // ─── Private helpers ──────────────────────────────────────────────────

        private static ParentUpcomingClass MapToUpcomingClass(CoachClass c) =>
            new ParentUpcomingClass
            {
                ClassId       = c.ClassId,
                StartDatetime = c.StartDatetime,
                EndDatetime   = c.EndDatetime,
                ModalityName  = c.IdModalityNavigation.Name,
                CoachName     = ResolveCoachName(c.IdCoachNavigation),
                StudioName    = c.IdStudioNavigation.Name,
                Status        = (CoachClassStatus)c.Status,
                StudentName   = string.Join(", ", c.Participants
                    .Select(p => ResolveStudentName(p.IdStudentNavigation)))
            };

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

        private static string ResolveUserName(User? user)
        {
            if (user is null) return string.Empty;
            var p = user.PersonInfo;
            return p is not null
                ? $"{p.FirstName} {p.LastName}".Trim()
                : user.Username;
        }

        private static int? ComputeAge(DateOnly? birthDate)
        {
            if (birthDate is null) return null;
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            int age = today.Year - birthDate.Value.Year;
            if (today < birthDate.Value.AddYears(age)) age--;
            return age;
        }
    }
}
