using DanceSchoolApp.Server.Data;
using DanceSchoolApp.Server.DTOs;
using DanceSchoolApp.Server.DTOs.Classes;
using DanceSchoolApp.Server.DTOs.Social;
using DanceSchoolApp.Server.Models;
using DanceSchoolApp.Server.Services.Social;
using Microsoft.EntityFrameworkCore;

namespace DanceSchoolApp.Server.Services.Classes
{
    public class CoachClassService
    {
        private readonly AppDbContext _context;
        private readonly NotificationService _notificationService;

        public CoachClassService(AppDbContext context,
            NotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
        }

        // ─── Queries ──────────────────────────────────────────────────────────

        public async Task<PagedResult<CoachClassListResponse>> GetAllAsync(PagedQuery query)
        {
            var dbQuery = _context.CoachClasses
                .Include(c => c.IdModalityNavigation)
                .Include(c => c.IdStudioNavigation)
                .Include(c => c.IdCoachNavigation)
                    .ThenInclude(coach => coach.CoachNavigation)
                        .ThenInclude(u => u.PersonInfo)
                .Include(c => c.Participants)
                .AsQueryable();

            var total = await dbQuery.CountAsync();

            var items = await dbQuery
                .OrderByDescending(c => c.StartDatetime)
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToListAsync();

            return new PagedResult<CoachClassListResponse>
            {
                Items = items.Select(MapToListResponse).ToList(),
                TotalCount = total,
                Page = query.Page,
                PageSize = query.PageSize
            };
        }

        public async Task<CoachClassDetailResponse> GetByIdAsync(int id)
        {
            var coachClass = await _context.CoachClasses
                .Include(c => c.IdModalityNavigation)
                .Include(c => c.IdStudioNavigation)
                .Include(c => c.IdCoachNavigation)
                    .ThenInclude(coach => coach.CoachNavigation)
                        .ThenInclude(u => u.PersonInfo)
                .Include(c => c.Participants)
                    .ThenInclude(p => p.IdStudentNavigation)
                        .ThenInclude(s => s.PersonInfo)
                .FirstOrDefaultAsync(c => c.ClassId == id);

            if (coachClass is null)
                throw new KeyNotFoundException($"Class with id {id} was not found.");

            return new CoachClassDetailResponse
            {
                ClassId = coachClass.ClassId,
                Status = (CoachClassStatus)coachClass.Status,
                CoachValidationStatus = (CoachValidationStatus)coachClass.CoachValidationStatus,
                StartDatetime = coachClass.StartDatetime,
                EndDatetime = coachClass.EndDatetime,
                ModalityId = coachClass.IdModality,
                ModalityName = coachClass.IdModalityNavigation.Name,
                StudioId = coachClass.IdStudio,
                StudioName = coachClass.IdStudioNavigation.Name,
                CoachId = coachClass.IdCoach,
                CoachName = ResolveCoachName(coachClass.IdCoachNavigation),
                CreatedByUserId = coachClass.CreatedBy,
                MaxParticipants = coachClass.MaxParticipants,
                CurrentParticipants = coachClass.Participants.Count,
                CreatedAt = coachClass.CreatedAt,
                CoachValidatedAt = coachClass.CoachValidatedAt,
                StaffValidatedAt = coachClass.StaffValidatedAt,
                Participants = coachClass.Participants.Select(p => new ClassParticipantSummary
                {
                    ParticipantId = p.ParticipantId,
                    StudentId = p.IdStudent,
                    StudentName = ResolveStudentName(p.IdStudentNavigation),
                    JoinedAt = p.JoinedAt,
                    ValidationStatus = p.ValidationStatus
                }).ToList()
            };
        }

        public async Task<List<OpenClassResponse>> GetOpenClassesAsync()
        {
            // Open = Approved status with available spots.
            // Materialise first because EF cannot translate the Count comparison
            // on a navigation collection to SQL in all versions.
            var classes = await _context.CoachClasses
                .Include(c => c.IdModalityNavigation)
                .Include(c => c.IdStudioNavigation)
                .Include(c => c.IdCoachNavigation)
                    .ThenInclude(coach => coach.CoachNavigation)
                        .ThenInclude(u => u.PersonInfo)
                .Include(c => c.Participants)
                .Where(c => c.Status == (byte)CoachClassStatus.Approved)
                .ToListAsync();

            return classes
                .Where(c => c.Participants.Count < c.MaxParticipants)
                .Select(c => new OpenClassResponse
                {
                    ClassId = c.ClassId,
                    StartDatetime = c.StartDatetime,
                    EndDatetime = c.EndDatetime,
                    ModalityName = c.IdModalityNavigation.Name,
                    StudioName = c.IdStudioNavigation.Name,
                    CoachName = ResolveCoachName(c.IdCoachNavigation),
                    MaxParticipants = c.MaxParticipants,
                    SpotsAvailable = c.MaxParticipants - c.Participants.Count
                })
                .ToList();
        }

        public async Task<List<CoachClassListResponse>> GetByStatusAsync(CoachClassStatus status)
        {
            var classes = await _context.CoachClasses
                .Include(c => c.IdModalityNavigation)
                .Include(c => c.IdStudioNavigation)
                .Include(c => c.IdCoachNavigation)
                    .ThenInclude(coach => coach.CoachNavigation)
                        .ThenInclude(u => u.PersonInfo)
                .Include(c => c.Participants)
                .Where(c => c.Status == (byte)status)
                .ToListAsync();

            return classes.Select(MapToListResponse).ToList();
        }

        public async Task<List<CoachClassListResponse>> GetByCoachAsync(int coachUserId)
        {
            bool coachExists = await _context.Coaches
                .AnyAsync(c => c.CoachId == coachUserId);

            if (!coachExists)
                throw new KeyNotFoundException($"Coach with id {coachUserId} was not found.");

            return await _context.CoachClasses
                .Include(c => c.IdModalityNavigation)
                .Include(c => c.IdStudioNavigation)
                .Include(c => c.IdCoachNavigation)
                    .ThenInclude(coach => coach.CoachNavigation)
                        .ThenInclude(u => u.PersonInfo)
                .Include(c => c.Participants)
                .Where(c => c.IdCoach == coachUserId)
                .Select(c => MapToListResponse(c))
                .ToListAsync();
        }

        public async Task<List<CoachClassListResponse>> GetByParentAsync(int parentUserId)
        {
            bool parentExists = await _context.Users
                .AnyAsync(u => u.UserId == parentUserId);

            if (!parentExists)
                throw new KeyNotFoundException($"User with id {parentUserId} was not found.");

            // Return classes where any enrolled student belongs to this parent.
            var classes = await _context.CoachClasses
                .Include(c => c.IdModalityNavigation)
                .Include(c => c.IdStudioNavigation)
                .Include(c => c.IdCoachNavigation)
                    .ThenInclude(coach => coach.CoachNavigation)
                        .ThenInclude(u => u.PersonInfo)
                .Include(c => c.Participants)
                    .ThenInclude(p => p.IdStudentNavigation)
                .Where(c => c.Participants
                    .Any(p => p.IdStudentNavigation.ParentUserId == parentUserId))
                .ToListAsync();

            return classes.Select(MapToListResponse).ToList();
        }

        // ─── Commands ─────────────────────────────────────────────────────────

        public async Task<int> CreateAsync(CoachClassCreateRequest request, int createdByUserId)
        {
            // ── 1. Validate all referenced entities exist ──────────────────────
            bool modalityActive = await _context.Modalities
                .AnyAsync(m => m.ModalityId == request.ModalityId && m.IsActive);

            if (!modalityActive)
                throw new KeyNotFoundException(
                    $"Modality with id {request.ModalityId} was not found or is inactive.");

            bool coachActive = await _context.Coaches
                .AnyAsync(c => c.CoachId == request.CoachId);

            if (!coachActive)
                throw new KeyNotFoundException(
                    $"Coach with id {request.CoachId} was not found.");

            // Ensure the coach actually teaches the requested modality
            bool coachTeachesModality = await _context.Coaches
                .Include(c => c.IdModalities)
                .Where(c => c.CoachId == request.CoachId)
                .AnyAsync(c => c.IdModalities.Any(m => m.ModalityId == request.ModalityId));

            if (!coachTeachesModality)
                throw new InvalidOperationException(
                    $"Coach with id {request.CoachId} does not teach modality {request.ModalityId}.");

            // Validate start/end chronology and duration limits (30 to 120 minutes)
            if (request.EndDatetime <= request.StartDatetime)
                throw new InvalidOperationException(
                    "The class end time must be after the start time.");

            var durationMinutes = (request.EndDatetime - request.StartDatetime).TotalMinutes;
            if (durationMinutes < 30 || durationMinutes > 120)
                throw new InvalidOperationException(
                    "Classes must have a duration between 30 and 120 minutes.");

            // ── 2. Auto-select studio ──────────────────────────────────────────
            var candidateStudios = await _context.Studios
                .Include(s => s.IdModalities)
                .Include(s => s.CoachClasses)
                .Where(s => s.IsActive && s.IdModalities.Any(m => m.ModalityId == request.ModalityId))
                .ToListAsync();

            if (!candidateStudios.Any())
                throw new InvalidOperationException(
                    $"No active studios support modality {request.ModalityId}.");

            var blockedStudioIds = await _context.BlockedPeriods
                .Where(b => b.Scope == 2 &&
                            b.StartDatetime < request.EndDatetime &&
                            b.EndDatetime > request.StartDatetime)
                .Select(b => b.IdStudio)
                .ToListAsync();

            var bookedStudioIds = await _context.CoachClasses
                .Where(c => c.Status != (byte)CoachClassStatus.Rejected &&
                            c.Status != (byte)CoachClassStatus.Cancelled &&
                            c.StartDatetime < request.EndDatetime &&
                            c.EndDatetime > request.StartDatetime)
                .Select(c => c.IdStudio)
                .ToListAsync();

            var requestDate = request.StartDatetime.Date;

            var selectedStudio = candidateStudios
                .Where(s => !blockedStudioIds.Contains(s.StudioId) &&
                            !bookedStudioIds.Contains(s.StudioId))
                .OrderBy(s => s.CoachClasses
                    .Count(c => c.StartDatetime.Date == requestDate &&
                                c.Status != (byte)CoachClassStatus.Cancelled &&
                                c.Status != (byte)CoachClassStatus.Rejected))
                .FirstOrDefault();

            if (selectedStudio is null)
                throw new InvalidOperationException(
                    "No studios are available for the requested time window and modality.");

            int assignedStudioId = selectedStudio.StudioId;

            // ── 3. Blocked period check ────────────────────────────────────────
            await CheckBlockedPeriodsAsync(
                request.StartDatetime, request.EndDatetime,
                request.CoachId, assignedStudioId);

            // ── 4. (Studio availability already handled above) ─────────────────

            // ── 5. Coach not double-booked ─────────────────────────────────────
            bool coachConflict = await _context.CoachClasses
                .AnyAsync(c =>
                    c.IdCoach == request.CoachId &&
                    c.Status != (byte)CoachClassStatus.Rejected &&
                    c.Status != (byte)CoachClassStatus.Cancelled &&
                    c.StartDatetime < request.EndDatetime &&
                    c.EndDatetime > request.StartDatetime);

            if (coachConflict)
                throw new InvalidOperationException(
                    "The coach is already booked during this time window.");

            // ── 5b. Coach availability covers the requested slot ───────────────
            var classDate    = DateOnly.FromDateTime(request.StartDatetime);
            var classWeekday = (byte)request.StartDatetime.DayOfWeek;
            var classStart   = TimeOnly.FromTimeSpan(request.StartDatetime.TimeOfDay);
            var classEnd     = TimeOnly.FromTimeSpan(request.EndDatetime.TimeOfDay);

            bool hasAvailability = await _context.CoachAvailabilities
                .AnyAsync(a =>
                    a.IdCoach   == request.CoachId  &&
                    a.Weekday   == classWeekday      &&
                    a.StartTime <= classStart        &&
                    a.EndTime   >= classEnd          &&
                    (a.ValidFrom  == null || a.ValidFrom  <= classDate) &&
                    (a.ValidUntil == null || a.ValidUntil >= classDate));

            if (!hasAvailability)
                throw new InvalidOperationException(
                    "The coach does not have an availability slot covering the requested time window.");

            // ── 6. Validate all student ids belong to this parent ──────────────
            var parentStudents = await _context.Students
                .Where(s => s.ParentUserId == createdByUserId && s.IsActive)
                .ToListAsync();

            var parentStudentIds = parentStudents.Select(s => s.StudentId).ToList();

            var invalidStudents = request.StudentIds.Except(parentStudentIds).ToList();
            if (invalidStudents.Any())
                throw new InvalidOperationException(
                    $"Student id(s) {string.Join(", ", invalidStudents)} do not belong " +
                    $"to parent {createdByUserId} or are inactive.");

            var notAccepted = parentStudents
                .Where(s => request.StudentIds.Contains(s.StudentId)
                         && s.AcceptanceStatus != 1)
                .Select(s => s.StudentId)
                .ToList();

            if (notAccepted.Any())
                throw new InvalidOperationException(
                    $"Student id(s) {string.Join(", ", notAccepted)} have not been " +
                    $"accepted by staff yet and cannot be enrolled in classes.");

            // ── 7. Create class + participants atomically ──────────────────────
            var coachClass = new CoachClass
            {
                IdModality = request.ModalityId,
                IdStudio = assignedStudioId,
                IdCoach = request.CoachId,
                CreatedBy = createdByUserId,
                StartDatetime = request.StartDatetime,
                EndDatetime = request.EndDatetime,
                MaxParticipants = request.MaxParticipants,
                Status = (byte)CoachClassStatus.Requested,
                CreatedAt = DateOnly.FromDateTime(DateTime.UtcNow)
            };

            _context.CoachClasses.Add(coachClass);

            // Save first to get ClassId, then add participants.
            // Both operations are inside the same EF change tracker so if
            // SaveChangesAsync fails the whole thing rolls back.
            await _context.SaveChangesAsync();

            foreach (var studentId in request.StudentIds)
            {
                _context.Participants.Add(new Participant
                {
                    IdCoachClass = coachClass.ClassId,
                    IdStudent = studentId,
                    JoinedAt = DateOnly.FromDateTime(DateTime.UtcNow),
                    ValidationStatus = 0  // pending validation
                });
            }

            await _context.SaveChangesAsync();

            return coachClass.ClassId;
        }

        // ─── Status transitions ───────────────────────────────────────────────

        public async Task StaffApproveAsync(int classId)
        {
            await TransitionStatusAsync(
                classId,
                allowedFrom: new[] { CoachClassStatus.Requested },
                newStatus: CoachClassStatus.StaffApproved,
                errorMessage: "Only a Requested class can be staff-approved."
            );

            var coachClass = await _context.CoachClasses
                .FirstOrDefaultAsync(c => c.ClassId == classId);

            if (coachClass is null) return;

            await _notificationService.SendAsync(
                userId: coachClass.IdCoach,
                title: "Class Request Awaiting Your Acceptance",
                message: $"A class has been scheduled for {coachClass.StartDatetime:dd/MM/yyyy HH:mm}. Please accept or reject it.",
                type: NotificationType.ClassUpdate,
                entityType: "CoachClass",
                entityId: classId);
        }

        public async Task CoachAcceptAsync(int classId, int coachUserId)
        {
            var coachClass = await _context.CoachClasses
                .FirstOrDefaultAsync(c => c.ClassId == classId);

            if (coachClass is null)
                throw new KeyNotFoundException($"Class with id {classId} was not found.");

            if (coachClass.IdCoach != coachUserId)
                throw new UnauthorizedAccessException("You are not the coach for this class.");

            if (coachClass.Status != (byte)CoachClassStatus.StaffApproved)
                throw new InvalidOperationException(
                    "Only StaffApproved classes can be accepted by the coach.");

            coachClass.Status = (byte)CoachClassStatus.Approved;
            await _context.SaveChangesAsync();

            await _notificationService.SendAsync(
                userId: coachClass.CreatedBy,
                title: "Class Approved",
                message: $"Your coaching class on {coachClass.StartDatetime:dd/MM/yyyy HH:mm} has been confirmed by the coach.",
                type: NotificationType.Success,
                entityType: "CoachClass",
                entityId: classId);

            //var staffIds = await _context.Users
            //    .Include(u => u.IdRoles)
            //    .Where(u => u.IdRoles.Any(r => r.RoleId == 1) && u.IsActive)
            //    .Select(u => u.UserId)
            //    .ToListAsync();

            //foreach (var staffId in staffIds)
            //{
            //    await _notificationService.SendAsync(
            //        userId: staffId,
            //        title: "Coach Accepted Class",
            //        message: $"Coach accepted class id {classId} on {coachClass.StartDatetime:dd/MM/yyyy HH:mm}.",
            //        type: NotificationType.ClassUpdate,
            //        entityType: "CoachClass",
            //        entityId: classId);
            //}
        }

        public async Task CoachRejectClassAsync(int classId, int coachUserId, string? reason)
        {
            var coachClass = await _context.CoachClasses
                .FirstOrDefaultAsync(c => c.ClassId == classId);

            if (coachClass is null)
                throw new KeyNotFoundException($"Class with id {classId} was not found.");

            if (coachClass.IdCoach != coachUserId)
                throw new UnauthorizedAccessException("You are not the coach for this class.");

            if (coachClass.Status != (byte)CoachClassStatus.StaffApproved)
                throw new InvalidOperationException(
                    "Only StaffApproved classes can be rejected by the coach.");

            coachClass.Status = (byte)CoachClassStatus.Rejected;
            await _context.SaveChangesAsync();

            await _notificationService.SendAsync(
                userId: coachClass.CreatedBy,
                title: "Class Rejected by Coach",
                message: reason is not null
                    ? $"Your class request was rejected by the coach. Reason: {reason}"
                    : "Your class request was rejected by the coach.",
                type: NotificationType.Warning,
                entityType: "CoachClass",
                entityId: classId);

            var staffIds = await _context.Users
                .Include(u => u.IdRoles)
                .Where(u => u.IdRoles.Any(r => r.RoleId == 1) && u.IsActive)
                .Select(u => u.UserId)
                .ToListAsync();

            foreach (var staffId in staffIds)
            {
                await _notificationService.SendAsync(
                    userId: staffId,
                    title: "Coach Rejected Class",
                    message: $"Coach rejected class id {classId}.",
                    type: NotificationType.Warning,
                    entityType: "CoachClass",
                    entityId: classId);
            }
        }

        public async Task RejectAsync(int classId, string? reason)
        {
            await TransitionStatusAsync(
                classId,
                allowedFrom: new[] { CoachClassStatus.Requested, CoachClassStatus.StaffApproved },
                newStatus: CoachClassStatus.Rejected,
                errorMessage: "Only a Requested or StaffApproved class can be rejected."
            );

            var coachClass = await _context.CoachClasses
                .FirstOrDefaultAsync(c => c.ClassId == classId);

            if (coachClass is null) return;

            await _notificationService.SendAsync(
                userId: coachClass.CreatedBy,
                title: "Class Rejected",
                message: reason is not null
                    ? $"Your class request was rejected. Reason: {reason}"
                    : "Your class request was rejected.",
                type: NotificationType.Warning,
                entityType: "CoachClass",
                entityId: classId);
        }

        public async Task CancelAsync(int classId)
        {
            await TransitionStatusAsync(
                classId,
                allowedFrom: new[] { CoachClassStatus.Requested, CoachClassStatus.Finished, CoachClassStatus.Approved, CoachClassStatus.Pending},
                newStatus: CoachClassStatus.Cancelled,
                errorMessage: "Only a Requested or Finished or Approved class can be cancelled."
            );

            var coachClass = await _context.CoachClasses
                .FirstOrDefaultAsync(c => c.ClassId == classId);

            if (coachClass is null) return;
            

            var parentsToNotify = coachClass.Participants
                .Where(p => p.IdCoachClass == classId)
                .Select(p => p.IdStudentNavigation.ParentUserId)
                .Distinct()
                .ToList();

            foreach (var parentId in parentsToNotify)
            {
                await _notificationService.SendAsync(
                   userId: parentId,
                   title: "Class Cancelled",
                   message: "A coaching class has been cancelled.",
                   type: NotificationType.Warning,
                   entityType: "CoachClass",
                   entityId: classId);
            }

            await _notificationService.SendAsync(
                userId: coachClass.IdCoach,
                title: "Class Cancelled",
                message: "A coaching class has been cancelled.",
                type: NotificationType.Warning,
                entityType: "CoachClass",
                entityId: classId);
        }

        public async Task FinishAsync(int classId)
        {
            await TransitionStatusAsync(
                classId,
                allowedFrom: new[] { CoachClassStatus.Approved },
                newStatus: CoachClassStatus.Finished,
                errorMessage: "Only an Approved class can be marked as finished."
            );

            var coachClass = await _context.CoachClasses
                .Include(c => c.Participants)
                    .ThenInclude(p => p.IdStudentNavigation)
                .FirstOrDefaultAsync(c => c.ClassId == classId);

            if (coachClass is null) return;

            coachClass.FinishedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            var distinctParentIds = coachClass.Participants
                .Select(p => p.IdStudentNavigation.ParentUserId)
                .Distinct();

            foreach (var parentId in distinctParentIds)
            {
                await _notificationService.SendAsync(
                    userId: parentId,
                    title: "Class Validation Required",
                    message: $"Please confirm attendance for the class on {coachClass.StartDatetime:dd/MM/yyyy HH:mm}. You have 48 hours to respond.",
                    type: NotificationType.ValidationRequest,
                    entityType: "CoachClass",
                    entityId: classId);
            }

            await _notificationService.SendAsync(
                userId: coachClass.IdCoach,
                title: "Class Validation Required",
                message: $"Please confirm you taught the class on {coachClass.StartDatetime:dd/MM/yyyy HH:mm}. You have 48 hours to respond.",
                type: NotificationType.ValidationRequest,
                entityType: "CoachClass",
                entityId: classId);
        }

        // ─── Validation workflow ──────────────────────────────────────────────

        public async Task CoachValidateAsync(int classId, int coachUserId, bool didTeach)
        {
            var coachClass = await _context.CoachClasses
                .Include(c => c.Participants)
                .FirstOrDefaultAsync(c => c.ClassId == classId);

            if (coachClass is null)
                throw new KeyNotFoundException($"Class with id {classId} was not found.");

            if (coachClass.IdCoach != coachUserId)
                throw new UnauthorizedAccessException("You are not the coach for this class.");

            if (coachClass.Status != (byte)CoachClassStatus.Finished &&
                coachClass.Status != (byte)CoachClassStatus.Pending)
                throw new InvalidOperationException(
                    "Coach validation is only available for Finished or Pending classes.");

            if (coachClass.CoachValidationStatus != (byte)CoachValidationStatus.Pending)
                throw new InvalidOperationException(
                    "Coach has already validated this class.");

            coachClass.CoachValidationStatus = didTeach
                ? (byte)CoachValidationStatus.Confirmed
                : (byte)CoachValidationStatus.Denied;
            coachClass.CoachValidatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            var notifTitle = didTeach ? "Coach Confirmed Class" : "Coach Denied Teaching Class";
            var notifMsg = didTeach
                ? $"Coach has confirmed class id {classId} was taught."
                : $"Coach has denied teaching class id {classId}.";

            var staffIds = await _context.Users
                .Include(u => u.IdRoles)
                .Where(u => u.IdRoles.Any(r => r.RoleId == 1) && u.IsActive)
                .Select(u => u.UserId)
                .ToListAsync();

            foreach (var staffId in staffIds)
            {
                await _notificationService.SendAsync(
                    userId: staffId,
                    title: notifTitle,
                    message: notifMsg,
                    type: NotificationType.ClassUpdate,
                    entityType: "CoachClass",
                    entityId: classId);
            }

            if (coachClass.Status == (byte)CoachClassStatus.Pending)
            {
                foreach (var staffId in staffIds)
                {
                    await _notificationService.SendAsync(
                        userId: staffId,
                        title: "Late Coach Validation Received",
                        message: $"Coach submitted a validation for class id {classId} after the validation window closed.",
                        type: NotificationType.Warning,
                        entityType: "CoachClass",
                        entityId: classId);
                }
            }

            // If all participants have already responded, advance to Pending now.
            await TryAdvanceClassToStaffReviewAsync(coachClass);
        }

        // Called after coach validates — mirrors the check in ParticipantService.
        // Advances to Pending only when BOTH the coach has responded AND all
        // participants have responded.
        private async Task TryAdvanceClassToStaffReviewAsync(CoachClass coachClass)
        {
            if (coachClass.Status != (byte)CoachClassStatus.Finished) return;
            if (coachClass.CoachValidationStatus == (byte)CoachValidationStatus.Pending) return;

            var allParticipants = await _context.Participants
                .Where(p => p.IdCoachClass == coachClass.ClassId)
                .ToListAsync();

            bool allResponded = allParticipants
                .All(p => p.ValidationStatus != 0); // 0 = ParticipantValidationStatus.Pending

            if (!allResponded) return;

            coachClass.Status = (byte)CoachClassStatus.Pending;
            await _context.SaveChangesAsync();

            var staffIds = await _context.Users
                .Include(u => u.IdRoles)
                .Where(u => u.IdRoles.Any(r => r.RoleId == 1) && u.IsActive)
                .Select(u => u.UserId)
                .ToListAsync();

            foreach (var staffId in staffIds)
            {
                await _notificationService.SendAsync(
                    userId: staffId,
                    title: "Class Ready for Validation",
                    message: $"Coach and all participants have responded for class id {coachClass.ClassId}. Final staff sign-off required.",
                    type: NotificationType.ValidationRequest,
                    entityType: "CoachClass",
                    entityId: coachClass.ClassId);
            }
        }

        public async Task StaffValidateAsync(int classId)
        {
            var coachClass = await _context.CoachClasses
                .Include(c => c.Participants)
                    .ThenInclude(p => p.IdStudentNavigation)
                .FirstOrDefaultAsync(c => c.ClassId == classId);

            if (coachClass is null)
                throw new KeyNotFoundException($"Class with id {classId} was not found.");

            if (coachClass.Status != (byte)CoachClassStatus.Pending)
                throw new InvalidOperationException(
                    "Staff validation is only available for Pending classes.");

            coachClass.StaffValidatedAt = DateTime.UtcNow;
            coachClass.Status = (byte)CoachClassStatus.Validated;
            await _context.SaveChangesAsync();

            var parentIds = coachClass.Participants
                .Select(p => p.IdStudentNavigation.ParentUserId)
                .Distinct();

            foreach (var parentId in parentIds)
            {
                await _notificationService.SendAsync(
                    userId: parentId,
                    title: "Class Validated",
                    message: $"The class on {coachClass.StartDatetime:dd/MM/yyyy HH:mm} has been fully validated.",
                    type: NotificationType.Success,
                    entityType: "CoachClass",
                    entityId: classId);
            }

            await _notificationService.SendAsync(
                userId: coachClass.IdCoach,
                title: "Class Validated",
                message: $"Your class on {coachClass.StartDatetime:dd/MM/yyyy HH:mm} has been validated by staff.",
                type: NotificationType.Success,
                entityType: "CoachClass",
                entityId: classId);
        }

        // ─── Private helpers ──────────────────────────────────────────────────

        private async Task CheckBlockedPeriodsAsync(
            DateTime start, DateTime end, int coachId, int studioId)
        {
            // Any global block (Undefined=0, NormalClass=1, Event=4, Holiday=5)
            // that overlaps the requested window blocks all bookings.
            bool globalBlock = await _context.BlockedPeriods
                .AnyAsync(b =>
                    (b.Scope == 0 || b.Scope == 1 || b.Scope == 4 || b.Scope == 5) &&
                    b.StartDatetime < end &&
                    b.EndDatetime > start);

            if (globalBlock)
                throw new InvalidOperationException(
                    "The requested time window falls within a global blocked period " +
                    "(school event, holiday, or normal class block).");

            // Studio-specific block
            bool studioBlock = await _context.BlockedPeriods
                .AnyAsync(b =>
                    b.Scope == 2 &&
                    b.IdStudio == studioId &&
                    b.StartDatetime < end &&
                    b.EndDatetime > start);

            if (studioBlock)
                throw new InvalidOperationException(
                    "The studio is blocked during this time window.");

            // Coach-specific block
            bool coachBlock = await _context.BlockedPeriods
                .AnyAsync(b =>
                    b.Scope == 3 &&
                    b.IdCoach == coachId &&
                    b.StartDatetime < end &&
                    b.EndDatetime > start);

            if (coachBlock)
                throw new InvalidOperationException(
                    "The coach is unavailable during this time window.");
        }

        // Generic status transition — enforces allowed-from states so invalid
        // transitions (e.g. approving an already-validated class) are caught
        // in one place rather than duplicated per method.
        private async Task TransitionStatusAsync(
            int classId,
            CoachClassStatus[] allowedFrom,
            CoachClassStatus newStatus,
            string errorMessage)
        {
            var coachClass = await _context.CoachClasses
                .FirstOrDefaultAsync(c => c.ClassId == classId);

            if (coachClass is null)
                throw new KeyNotFoundException($"Class with id {classId} was not found.");

            var currentStatus = (CoachClassStatus)coachClass.Status;

            if (!allowedFrom.Contains(currentStatus))
                throw new InvalidOperationException(errorMessage);

            coachClass.Status = (byte)newStatus;
            await _context.SaveChangesAsync();
        }

        private static CoachClassListResponse MapToListResponse(CoachClass c) =>
            new CoachClassListResponse
            {
                ClassId = c.ClassId,
                Status = (CoachClassStatus)c.Status,
                CoachValidationStatus = (CoachValidationStatus)c.CoachValidationStatus,
                StartDatetime = c.StartDatetime,
                EndDatetime = c.EndDatetime,
                ModalityName = c.IdModalityNavigation.Name,
                StudioName = c.IdStudioNavigation.Name,
                CoachName = ResolveCoachName(c.IdCoachNavigation),
                MaxParticipants = c.MaxParticipants,
                CurrentParticipants = c.Participants.Count,
                CreatedAt = c.CreatedAt
            };

        private static string ResolveCoachName(Coach coach)
        {
            var person = coach.CoachNavigation?.PersonInfo;
            return person is not null
                ? $"{person.FirstName} {person.LastName}".Trim()
                : coach.CoachNavigation?.Username ?? $"Coach {coach.CoachId}";
        }

        private static string ResolveStudentName(Student student)
        {
            var person = student.PersonInfo;
            return person is not null
                ? $"{person.FirstName} {person.LastName}".Trim()
                : $"Student {student.StudentId}";
        }
    }
}
