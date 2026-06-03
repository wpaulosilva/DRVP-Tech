using DanceSchoolApp.Server.Data;
using DanceSchoolApp.Server.DTOs;
using DanceSchoolApp.Server.DTOs.Classes;
using DanceSchoolApp.Server.DTOs.Social;
using DanceSchoolApp.Server.Models;
using DanceSchoolApp.Server.Services;
using DanceSchoolApp.Server.Services.Social;
using Microsoft.EntityFrameworkCore;

namespace DanceSchoolApp.Server.Services.Classes
{
    public class CoachClassService
    {
        private readonly AppDbContext _context;
        private readonly NotificationService _notificationService;
        private readonly AppSettingService _appSettingService;

        public CoachClassService(AppDbContext context,
            NotificationService notificationService,
            AppSettingService appSettingService)
        {
            _context = context;
            _notificationService = notificationService;
            _appSettingService = appSettingService;
        }

        //  Queries

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
                ClassOrigin = (ClassOrigin)coachClass.ClassOrigin,
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
                    ValidationStatus = p.ValidationStatus,
                    ParentEnrollmentStatus = p.ParentEnrollmentStatus
                }).ToList()
            };
        }

        public async Task<List<OpenClassResponse>> GetOpenClassesAsync()
        {
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

        public async Task<bool> IsParentOfClassAsync(int parentUserId, int classId)
        {
            return await _context.Participants
                .Include(p => p.IdStudentNavigation)
                .AnyAsync(p => p.IdCoachClass == classId
                            && p.IdStudentNavigation.ParentUserId == parentUserId);
        }

        public async Task<List<CoachClassListResponse>> GetByParentAsync(int parentUserId)
        {
            bool parentExists = await _context.Users
                .AnyAsync(u => u.UserId == parentUserId);

            if (!parentExists)
                throw new KeyNotFoundException($"User with id {parentUserId} was not found.");

            var classes = await _context.CoachClasses
                .Include(c => c.IdModalityNavigation)
                .Include(c => c.IdStudioNavigation)
                .Include(c => c.IdCoachNavigation)
                    .ThenInclude(coach => coach.CoachNavigation)
                        .ThenInclude(u => u.PersonInfo)
                .Include(c => c.Participants)
                    .ThenInclude(p => p.IdStudentNavigation)
                        .ThenInclude(s => s.PersonInfo)
                .Where(c => c.Participants
                    .Any(p => p.IdStudentNavigation.ParentUserId == parentUserId))
                .ToListAsync();

            return classes.Select(MapToListResponse).ToList();
        }

        //  Commands

        // Parent creates an individual class for one of their own students.
        // MaxParticipants is always 1. Flow: Requested → CoachApproved → Approved.
        public async Task<int> ParentCreateAsync(CoachClassParentCreateRequest request, int parentUserId)
        {
            await ValidateModalityAndCoach(request.ModalityId, request.CoachId);
            ValidateDuration(request.StartDatetime, request.EndDatetime);

            int assignedStudioId = await SelectStudioAsync(request.ModalityId, request.StartDatetime, request.EndDatetime);

            await CheckBlockedPeriodsAsync(request.StartDatetime, request.EndDatetime, request.CoachId, assignedStudioId);
            await CheckCoachConflictAsync(request.CoachId, request.StartDatetime, request.EndDatetime);
            await CheckCoachAvailabilityAsync(request.CoachId, request.StartDatetime, request.EndDatetime);

            // Student must belong to calling parent, be active, accepted, and have the modality
            var student = await _context.Students
                .Include(s => s.IdModalities)
                .FirstOrDefaultAsync(s => s.StudentId == request.StudentId
                                       && s.ParentUserId == parentUserId
                                       && s.IsActive);

            if (student is null)
                throw new KeyNotFoundException(
                    $"Student with id {request.StudentId} was not found, does not belong to you, or is inactive.");

            if (student.AcceptanceStatus != 1)
                throw new InvalidOperationException(
                    $"Student {request.StudentId} has not been accepted by staff yet and cannot be enrolled in classes.");

            if (!student.IdModalities.Any(m => m.ModalityId == request.ModalityId))
                throw new InvalidOperationException(
                    $"Student {request.StudentId} is not assigned to modality {request.ModalityId}.");

            await CheckStudentTimeConflictAsync(request.StudentId, request.StartDatetime, request.EndDatetime);

            using var tx = await _context.Database.BeginTransactionAsync();

            var coachClass = new CoachClass
            {
                IdModality      = request.ModalityId,
                IdStudio        = assignedStudioId,
                IdCoach         = request.CoachId,
                CreatedBy       = parentUserId,
                StartDatetime   = request.StartDatetime,
                EndDatetime     = request.EndDatetime,
                MaxParticipants = 1,
                Status          = (byte)CoachClassStatus.Requested,
                ClassOrigin     = (byte)ClassOrigin.ParentCreated,
                CreatedAt       = DateOnly.FromDateTime(DateTime.Now)
            };

            _context.CoachClasses.Add(coachClass);
            await _context.SaveChangesAsync();

            decimal defaultPrice = await ComputeDefaultPriceAsync(coachClass.StartDatetime);

            _context.Participants.Add(new Participant
            {
                IdCoachClass           = coachClass.ClassId,
                IdStudent              = request.StudentId,
                JoinedAt               = DateOnly.FromDateTime(DateTime.Now),
                ValidationStatus       = (byte)ParticipantValidationStatus.Pending,
                ParentEnrollmentStatus = (byte)ParentEnrollmentStatus.NotRequired,
                PerParticipantPrice    = defaultPrice
            });

            await _context.SaveChangesAsync();
            await tx.CommitAsync();

            // Notify coach of the new request
            await _notificationService.SendAsync(
                userId: request.CoachId,
                title: "Novo pedido de aula",
                message: $"Um encarregado pediu uma aula para {request.StartDatetime:dd/MM/yyyy 'às' HH:mm}. Por favor, aceite ou rejeite.",
                type: NotificationType.ClassUpdate,
                entityType: "CoachClass",
                entityId: coachClass.ClassId);

            return coachClass.ClassId;
        }

        // Coach creates an individual or group class, selecting students with the requested modality.
        // Flow: Requested → [all parents approve enrollment] → CoachApproved → Approved.
        public async Task<int> CoachCreateAsync(CoachClassCoachCreateRequest request, int coachUserId)
        {
            bool coachActive = await _context.Coaches
                .AnyAsync(c => c.CoachId == coachUserId);

            if (!coachActive)
                throw new KeyNotFoundException($"Coach with id {coachUserId} was not found.");

            bool modalityActive = await _context.Modalities
                .AnyAsync(m => m.ModalityId == request.ModalityId && m.IsActive);

            if (!modalityActive)
                throw new KeyNotFoundException(
                    $"Modality with id {request.ModalityId} was not found or is inactive.");

            bool coachTeachesModality = await _context.Coaches
                .Include(c => c.IdModalities)
                .Where(c => c.CoachId == coachUserId)
                .AnyAsync(c => c.IdModalities.Any(m => m.ModalityId == request.ModalityId));

            if (!coachTeachesModality)
                throw new InvalidOperationException(
                    $"Coach {coachUserId} does not teach modality {request.ModalityId}.");

            ValidateDuration(request.StartDatetime, request.EndDatetime);

            int maxAllowed = await _appSettingService.GetIntAsync("max_participants", 8);
            if (request.MaxParticipants > maxAllowed)
                throw new InvalidOperationException(
                    $"MaxParticipants cannot exceed the configured limit of {maxAllowed}.");

            int assignedStudioId = await SelectStudioAsync(request.ModalityId, request.StartDatetime, request.EndDatetime);

            await CheckBlockedPeriodsAsync(request.StartDatetime, request.EndDatetime, coachUserId, assignedStudioId);
            await CheckCoachConflictAsync(coachUserId, request.StartDatetime, request.EndDatetime);
            await CheckCoachAvailabilityAsync(coachUserId, request.StartDatetime, request.EndDatetime);

            // Validate each student: active, accepted, has the modality
            var students = await _context.Students
                .Include(s => s.IdModalities)
                .Where(s => request.StudentIds.Contains(s.StudentId) && s.IsActive)
                .ToListAsync();

            var missingStudents = request.StudentIds
                .Except(students.Select(s => s.StudentId))
                .ToList();

            if (missingStudents.Any())
                throw new KeyNotFoundException(
                    $"Student id(s) {string.Join(", ", missingStudents)} were not found or are inactive.");

            var notAccepted = students
                .Where(s => s.AcceptanceStatus != 1)
                .Select(s => s.StudentId)
                .ToList();

            if (notAccepted.Any())
                throw new InvalidOperationException(
                    $"Student id(s) {string.Join(", ", notAccepted)} have not been accepted by staff yet.");

            var notInModality = students
                .Where(s => !s.IdModalities.Any(m => m.ModalityId == request.ModalityId))
                .Select(s => s.StudentId)
                .ToList();

            if (notInModality.Any())
                throw new InvalidOperationException(
                    $"Student id(s) {string.Join(", ", notInModality)} are not assigned to modality {request.ModalityId}.");

            foreach (var studentId in request.StudentIds)
                await CheckStudentTimeConflictAsync(studentId, request.StartDatetime, request.EndDatetime);

            using var tx = await _context.Database.BeginTransactionAsync();

            var coachClass = new CoachClass
            {
                IdModality      = request.ModalityId,
                IdStudio        = assignedStudioId,
                IdCoach         = coachUserId,
                CreatedBy       = coachUserId,
                StartDatetime   = request.StartDatetime,
                EndDatetime     = request.EndDatetime,
                MaxParticipants = request.MaxParticipants,
                Status          = (byte)CoachClassStatus.Requested,
                ClassOrigin     = (byte)ClassOrigin.CoachCreated,
                CreatedAt       = DateOnly.FromDateTime(DateTime.Now)
            };

            _context.CoachClasses.Add(coachClass);
            await _context.SaveChangesAsync();

            decimal defaultPrice = await ComputeDefaultPriceAsync(coachClass.StartDatetime);

            foreach (var studentId in request.StudentIds)
            {
                _context.Participants.Add(new Participant
                {
                    IdCoachClass           = coachClass.ClassId,
                    IdStudent              = studentId,
                    JoinedAt               = DateOnly.FromDateTime(DateTime.Now),
                    ValidationStatus       = (byte)ParticipantValidationStatus.Pending,
                    ParentEnrollmentStatus = (byte)ParentEnrollmentStatus.Pending,
                    PerParticipantPrice    = defaultPrice
                });
            }

            await _context.SaveChangesAsync();
            await tx.CommitAsync();

            // Notify each student's parent to approve enrollment
            var enrolledStudents = await _context.Students
                .Include(s => s.PersonInfo)
                .Where(s => request.StudentIds.Contains(s.StudentId))
                .ToListAsync();

            var scheduledAt = coachClass.StartDatetime.ToString("dd/MM/yyyy 'às' HH:mm");

            var parentIds = enrolledStudents
                .Select(s => s.ParentUserId)
                .Distinct();

            foreach (var parentId in parentIds)
            {
                await _notificationService.SendAsync(
                    userId: parentId,
                    title: "Inscrição em aula pelo professor",
                    message: $"O seu educando foi inscrito numa aula de {scheduledAt}. Por favor confirme a sua participação.",
                    type: NotificationType.ValidationRequest,
                    entityType: "CoachClass",
                    entityId: coachClass.ClassId);
            }

            return coachClass.ClassId;
        }

        // Coach responds to a parent-created class: Requested → CoachApproved or Rejected.
        // Blocked for coach-created classes (no coach approval step there).
        public async Task CoachRespondAsync(int classId, int coachUserId, bool accept, string? reason)
        {
            var coachClass = await _context.CoachClasses
                .FirstOrDefaultAsync(c => c.ClassId == classId);

            if (coachClass is null)
                throw new KeyNotFoundException($"Class with id {classId} was not found.");

            if (coachClass.ClassOrigin == (byte)ClassOrigin.CoachCreated)
                throw new InvalidOperationException(
                    "This class is coach-created. Coach approval is not applicable — parents approve enrollment instead.");

            if (coachClass.IdCoach != coachUserId)
                throw new UnauthorizedAccessException("You are not the coach for this class.");

            if (coachClass.StartDatetime <= DateTime.Now)
                throw new InvalidOperationException(
                    "It is not possible to approve or reject classes whose date has already passed.");

            if (coachClass.Status != (byte)CoachClassStatus.Requested)
                throw new InvalidOperationException(
                    "Only Requested classes can be responded to by the coach.");

            coachClass.Status = accept
                ? (byte)CoachClassStatus.CoachApproved
                : (byte)CoachClassStatus.Rejected;

            await _context.SaveChangesAsync();

            if (accept)
            {
                var scheduledAt = coachClass.StartDatetime.ToString("dd/MM/yyyy 'às' HH:mm");

                var staffIds = await _context.Users
                    .Include(u => u.IdRoles)
                    .Where(u => u.IdRoles.Any(r => r.RoleId == 1) && u.IsActive)
                    .Select(u => u.UserId)
                    .ToListAsync();

                foreach (var staffId in staffIds)
                {
                    await _notificationService.SendAsync(
                        userId: staffId,
                        title: "Aula aprovada pelo professor",
                        message: $"O professor aceitou a aula de {scheduledAt}. Por favor, aprove ou rejeite este pedido.",
                        type: NotificationType.Success,
                        entityType: "CoachClass",
                        entityId: classId);
                }
            }
            else
            {
                await _notificationService.SendAsync(
                    userId: coachClass.CreatedBy,
                    title: "Aula rejeitada pelo professor",
                    message: reason is not null
                        ? $"O seu pedido de aula a {coachClass.StartDatetime:dd/MM/yyyy HH:mm} foi rejeitado pelo professor. Razão: {reason}"
                        : $"O seu pedido de aula a {coachClass.StartDatetime:dd/MM/yyyy HH:mm} foi rejeitado pelo professor.",
                    type: NotificationType.Warning,
                    entityType: "CoachClass",
                    entityId: classId);
            }
        }

        // Staff responds second: CoachApproved → Approved (approve) or Rejected (reject).
        public async Task StaffRespondAsync(int classId, bool approve, string? reason, decimal? perParticipantPrice = null)
        {
            var coachClass = await _context.CoachClasses
                .Include(c => c.Participants)
                .FirstOrDefaultAsync(c => c.ClassId == classId);

            if (coachClass is null)
                throw new KeyNotFoundException($"Class with id {classId} was not found.");

            if (coachClass.StartDatetime <= DateTime.Now)
                throw new InvalidOperationException(
                    "It is not possible to approve or reject classes whose date has already passed.");

            await TransitionStatusAsync(
                classId,
                allowedFrom: new[] { CoachClassStatus.CoachApproved },
                newStatus: approve ? CoachClassStatus.Approved : CoachClassStatus.Rejected,
                errorMessage: "Only a CoachApproved class can be responded to by staff."
            );

            if (!approve)
            {
                await _notificationService.SendAsync(
                    userId: coachClass.CreatedBy,
                    title: "Aula rejeitada pelo staff",
                    message: reason is not null
                        ? $"O pedido de aula foi rejeitado pelo staff. Razão: {reason}"
                        : "O pedido de aula foi rejeitado pelo staff.",
                    type: NotificationType.Warning,
                    entityType: "CoachClass",
                    entityId: classId);

                // Also notify coach when a coach-created class is rejected
                if (coachClass.ClassOrigin == (byte)ClassOrigin.CoachCreated)
                {
                    await _notificationService.SendAsync(
                        userId: coachClass.IdCoach,
                        title: "Aula rejeitada pelo staff",
                        message: reason is not null
                            ? $"A sua aula de {coachClass.StartDatetime:dd/MM/yyyy HH:mm} foi rejeitada pelo staff. Razão: {reason}"
                            : $"A sua aula de {coachClass.StartDatetime:dd/MM/yyyy HH:mm} foi rejeitada pelo staff.",
                        type: NotificationType.Warning,
                        entityType: "CoachClass",
                        entityId: classId);
                }
            }
            else
            {
                var scheduledAt = coachClass.StartDatetime.ToString("dd/MM/yyyy 'às' HH:mm");

                // For parent-created: notify the parent. For coach-created: notify the coach.
                await _notificationService.SendAsync(
                    userId: coachClass.CreatedBy,
                    title: "Aula aprovada",
                    message: $"A aula agendada para {scheduledAt} foi aprovada pelo staff e está confirmada.",
                    type: NotificationType.Success,
                    entityType: "CoachClass",
                    entityId: classId);
            }

            // If staff provided a per-participant price override, apply to all participants
            if (approve && perParticipantPrice.HasValue)
            {
                foreach (var p in coachClass.Participants)
                    p.PerParticipantPrice = perParticipantPrice.Value;
                await _context.SaveChangesAsync();
            }
        }

        public async Task UpdateDetailsAsync(int classId, CoachClassUpdateDetailsRequest request)
        {
            var coachClass = await _context.CoachClasses
                .Include(c => c.Participants)
                    .ThenInclude(p => p.IdStudentNavigation)
                .FirstOrDefaultAsync(c => c.ClassId == classId);

            if (coachClass is null)
                throw new KeyNotFoundException($"Class with id {classId} was not found.");

            bool changed = false;

            if (request.StudioId.HasValue)
            {
                coachClass.IdStudio = request.StudioId.Value;
                changed = true;
            }

            if (request.StartDatetime.HasValue)
            {
                coachClass.StartDatetime = request.StartDatetime.Value;
                changed = true;
            }

            if (request.EndDatetime.HasValue)
            {
                if (request.EndDatetime.Value <= (request.StartDatetime ?? coachClass.StartDatetime))
                    throw new InvalidOperationException("EndDatetime must be after StartDatetime.");
                coachClass.EndDatetime = request.EndDatetime.Value;
                changed = true;
            }

            if (request is not null && request.PerParticipantPrice.HasValue)
            {
                foreach (var p in coachClass.Participants)
                    p.PerParticipantPrice = request.PerParticipantPrice.Value;
                changed = true;
            }

            await _context.SaveChangesAsync();

            if (changed)
            {
                var scheduledAt = coachClass.StartDatetime.ToString("dd/MM/yyyy 'às' HH:mm");

                // Notify coach
                await _notificationService.SendAsync(
                    userId: coachClass.IdCoach,
                    title: "Aula atualizada pelo staff",
                    message: $"Os detalhes da sua aula de {scheduledAt} foram alterados pelo staff.",
                    type: NotificationType.ClassUpdate,
                    entityType: "CoachClass",
                    entityId: classId);

                // Notify parents of active participants
                var parentIds = coachClass.Participants
                    .Where(p => p.ParentEnrollmentStatus != (byte)ParentEnrollmentStatus.Rejected)
                    .Select(p => p.IdStudentNavigation.ParentUserId)
                    .Distinct();

                foreach (var parentId in parentIds)
                {
                    if (parentId == coachClass.IdCoach) continue; // skip if coach is also the creator

                    await _notificationService.SendAsync(
                        userId: parentId,
                        title: "Aula atualizada pelo staff",
                        message: $"Os detalhes da aula agendada para {scheduledAt} foram alterados pelo staff.",
                        type: NotificationType.ClassUpdate,
                        entityType: "CoachClass",
                        entityId: classId);
                }
            }
        }

        public async Task CancelAsync(int classId)
        {
            await TransitionStatusAsync(
                classId,
                allowedFrom: new[] { CoachClassStatus.Requested, CoachClassStatus.CoachApproved, CoachClassStatus.Finished, CoachClassStatus.Approved, CoachClassStatus.Pending },
                newStatus: CoachClassStatus.Cancelled,
                errorMessage: "Only a Requested, CoachApproved, Finished, or Approved class can be cancelled."
            );

            var coachClass = await _context.CoachClasses
                .Include(c => c.Participants)
                    .ThenInclude(p => p.IdStudentNavigation)
                .FirstOrDefaultAsync(c => c.ClassId == classId);

            if (coachClass is null) return;

            var scheduledAt = coachClass.StartDatetime.ToString("dd/MM/yyyy 'às' HH:mm");

            var parentsToNotify = coachClass.Participants
                .Where(p => p.ParentEnrollmentStatus != (byte)ParentEnrollmentStatus.Rejected)
                .Select(p => p.IdStudentNavigation.ParentUserId)
                .Distinct()
                .ToList();

            foreach (var parentId in parentsToNotify)
            {
                await _notificationService.SendAsync(
                    userId: parentId,
                    title: "Aula cancelada",
                    message: $"A aula agendada para {scheduledAt} foi cancelada.",
                    type: NotificationType.Warning,
                    entityType: "CoachClass",
                    entityId: classId);
            }

            await _notificationService.SendAsync(
                userId: coachClass.IdCoach,
                title: "Aula cancelada",
                message: $"A aula agendada para {scheduledAt} foi cancelada.",
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

            coachClass.FinishedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            // Only notify parents of active (non-rejected) participants
            var distinctParentIds = coachClass.Participants
                .Where(p => p.ParentEnrollmentStatus != (byte)ParentEnrollmentStatus.Rejected)
                .Select(p => p.IdStudentNavigation.ParentUserId)
                .Distinct();

            foreach (var parentId in distinctParentIds)
            {
                await _notificationService.SendAsync(
                    userId: parentId,
                    title: "Aula terminada - validação necessária",
                    message: $"Por favor confirme a presença na aula de {coachClass.StartDatetime:dd/MM/yyyy 'às' HH:mm}. Tem 48 horas para responder.",
                    type: NotificationType.ValidationRequest,
                    entityType: "CoachClass",
                    entityId: classId);
            }

            await _notificationService.SendAsync(
                userId: coachClass.IdCoach,
                title: "Aula terminada - validação necessária",
                message: $"Por favor confirme que lecionou a aula de {coachClass.StartDatetime:dd/MM/yyyy 'às' HH:mm}. Tem 48 horas para responder.",
                type: NotificationType.ValidationRequest,
                entityType: "CoachClass",
                entityId: classId);
        }

        //  Validation workflow

        public async Task CoachValidateAsync(int classId, int coachUserId, bool didTeach)
        {
            var coachClass = await _context.CoachClasses
                .Include(c => c.Participants)
                .FirstOrDefaultAsync(c => c.ClassId == classId);

            if (coachClass is null)
                throw new KeyNotFoundException($"Class with id {classId} was not found.");

            if (coachClass.IdCoach != coachUserId)
                throw new UnauthorizedAccessException("You are not the coach for this class.");

            if (coachClass.Status != (byte)CoachClassStatus.Finished)
                throw new InvalidOperationException(
                    "Coach validation is only available for Finished classes.");

            if (coachClass.CoachValidationStatus != (byte)CoachValidationStatus.Pending)
                throw new InvalidOperationException(
                    "Coach has already validated this class.");

            coachClass.CoachValidationStatus = didTeach
                ? (byte)CoachValidationStatus.Confirmed
                : (byte)CoachValidationStatus.Denied;
            coachClass.CoachValidatedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            // Only count active participants for the "all responded" check
            bool allResponded = coachClass.Participants
                .Where(p => p.ParentEnrollmentStatus != (byte)ParentEnrollmentStatus.Rejected)
                .All(p => p.ValidationStatus != (byte)ParticipantValidationStatus.Pending);

            if (allResponded)
            {
                coachClass.Status = (byte)CoachClassStatus.Pending;
                await _context.SaveChangesAsync();
            }
        }

        public async Task StaffValidateAsync(int classId, bool confirmed, string? reason, decimal? perParticipantPrice = null)
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

            coachClass.StaffValidatedAt = DateTime.Now;
            coachClass.Status = confirmed
                ? (byte)CoachClassStatus.Validated
                : (byte)CoachClassStatus.Cancelled;

            // Apply per-participant price override if provided when confirming validation
            if (confirmed && perParticipantPrice.HasValue)
            {
                foreach (var p in coachClass.Participants)
                    p.PerParticipantPrice = perParticipantPrice.Value;
            }

            await _context.SaveChangesAsync();

            var parentIds = coachClass.Participants
                .Where(p => p.ParentEnrollmentStatus != (byte)ParentEnrollmentStatus.Rejected)
                .Select(p => p.IdStudentNavigation.ParentUserId)
                .Distinct();

            if (!confirmed)
            {
                var notifMsg = reason is not null
                 ? $"A aula agendada para {coachClass.StartDatetime:dd/MM/yyyy 'às' HH:mm} não foi confirmada pelo staff. Razão: {reason}"
                 : $"A aula agendada para {coachClass.StartDatetime:dd/MM/yyyy 'às' HH:mm} não foi confirmada pelo staff.";

                foreach (var parentId in parentIds)
                {
                    await _notificationService.SendAsync(
                        userId: parentId,
                        title: "Aula não confirmada",
                        message: notifMsg,
                        type: NotificationType.Warning,
                        entityType: "CoachClass",
                        entityId: classId);
                }

                await _notificationService.SendAsync(
                    userId: coachClass.IdCoach,
                    title: "Aula não confirmada",
                    message: notifMsg,
                    type: NotificationType.Warning,
                    entityType: "CoachClass",
                    entityId: classId);
            }
        }

        //  Private helpers

        private async Task ValidateModalityAndCoach(int modalityId, int coachId)
        {
            bool modalityActive = await _context.Modalities
                .AnyAsync(m => m.ModalityId == modalityId && m.IsActive);

            if (!modalityActive)
                throw new KeyNotFoundException(
                    $"Modality with id {modalityId} was not found or is inactive.");

            bool coachActive = await _context.Coaches
                .AnyAsync(c => c.CoachId == coachId);

            if (!coachActive)
                throw new KeyNotFoundException(
                    $"Coach with id {coachId} was not found.");

            bool coachTeachesModality = await _context.Coaches
                .Include(c => c.IdModalities)
                .Where(c => c.CoachId == coachId)
                .AnyAsync(c => c.IdModalities.Any(m => m.ModalityId == modalityId));

            if (!coachTeachesModality)
                throw new InvalidOperationException(
                    $"Coach with id {coachId} does not teach modality {modalityId}.");
        }

        private static void ValidateDuration(DateTime start, DateTime end)
        {
            if (end <= start)
                throw new InvalidOperationException(
                    "The class end time must be after the start time.");

            var durationMinutes = (end - start).TotalMinutes;
            if (durationMinutes < 30 || durationMinutes > 120)
                throw new InvalidOperationException(
                    "Classes must have a duration between 30 and 120 minutes.");
        }

        private async Task<int> SelectStudioAsync(int modalityId, DateTime start, DateTime end)
        {
            var candidateStudios = await _context.Studios
                .Include(s => s.IdModalities)
                .Include(s => s.CoachClasses)
                .Where(s => s.IsActive && s.IdModalities.Any(m => m.ModalityId == modalityId))
                .ToListAsync();

            if (!candidateStudios.Any())
                throw new InvalidOperationException(
                    $"No active studios support modality {modalityId}.");

            var blockedStudioIds = await _context.BlockedPeriods
                .Where(b => b.Scope == 2 && b.StartDatetime < end && b.EndDatetime > start)
                .Select(b => b.IdStudio)
                .ToListAsync();

            var bookedStudioIds = await _context.CoachClasses
                .Where(c => c.Status != (byte)CoachClassStatus.Rejected &&
                            c.Status != (byte)CoachClassStatus.Cancelled &&
                            c.StartDatetime < end && c.EndDatetime > start)
                .Select(c => c.IdStudio)
                .ToListAsync();

            var requestDate = start.Date;

            var selected = candidateStudios
                .Where(s => !blockedStudioIds.Contains(s.StudioId) &&
                            !bookedStudioIds.Contains(s.StudioId))
                .OrderBy(s => s.CoachClasses
                    .Count(c => c.StartDatetime.Date == requestDate &&
                                c.Status != (byte)CoachClassStatus.Cancelled &&
                                c.Status != (byte)CoachClassStatus.Rejected))
                .FirstOrDefault();

            if (selected is null)
                throw new InvalidOperationException(
                    "No studios are available for the requested time window and modality.");

            return selected.StudioId;
        }

        private async Task CheckCoachConflictAsync(int coachId, DateTime start, DateTime end)
        {
            bool coachConflict = await _context.CoachClasses
                .AnyAsync(c =>
                    c.IdCoach == coachId &&
                    c.Status != (byte)CoachClassStatus.Rejected &&
                    c.Status != (byte)CoachClassStatus.Cancelled &&
                    c.StartDatetime < end && c.EndDatetime > start);

            if (coachConflict)
                throw new InvalidOperationException(
                    "The coach is already booked during this time window.");
        }

        private async Task CheckCoachAvailabilityAsync(int coachId, DateTime start, DateTime end)
        {
            var classDate    = DateOnly.FromDateTime(start);
            var classWeekday = (byte)start.DayOfWeek;
            var classStart   = TimeOnly.FromTimeSpan(start.TimeOfDay);
            var classEnd     = TimeOnly.FromTimeSpan(end.TimeOfDay);

            bool hasAvailability = await _context.CoachAvailabilities
                .AnyAsync(a =>
                    a.IdCoach   == coachId   &&
                    a.Weekday   == classWeekday &&
                    a.StartTime <= classStart   &&
                    a.EndTime   >= classEnd     &&
                    (a.ValidFrom  == null || a.ValidFrom  <= classDate) &&
                    (a.ValidUntil == null || a.ValidUntil >= classDate));

            if (!hasAvailability)
                throw new InvalidOperationException(
                    "The coach does not have an availability slot covering the requested time window.");
        }

        private async Task CheckStudentTimeConflictAsync(int studentId, DateTime start, DateTime end)
        {
            bool timeConflict = await _context.Participants
                .Include(p => p.IdCoachClassNavigation)
                .AnyAsync(p =>
                    p.IdStudent == studentId &&
                    p.ParentEnrollmentStatus != (byte)ParentEnrollmentStatus.Rejected &&
                    p.IdCoachClassNavigation.Status != (byte)CoachClassStatus.Rejected &&
                    p.IdCoachClassNavigation.Status != (byte)CoachClassStatus.Cancelled &&
                    p.IdCoachClassNavigation.StartDatetime < end &&
                    p.IdCoachClassNavigation.EndDatetime > start);

            if (timeConflict)
                throw new InvalidOperationException(
                    $"Student {studentId} is already enrolled in another class at this time.");
        }

        private async Task CheckBlockedPeriodsAsync(
            DateTime start, DateTime end, int coachId, int studioId)
        {
            bool globalBlock = await _context.BlockedPeriods
                .AnyAsync(b =>
                    (b.Scope == 0 || b.Scope == 1 || b.Scope == 4 || b.Scope == 5) &&
                    b.StartDatetime < end && b.EndDatetime > start);

            if (globalBlock)
                throw new InvalidOperationException(
                    "The requested time window falls within a global blocked period.");

            bool studioBlock = await _context.BlockedPeriods
                .AnyAsync(b =>
                    b.Scope == 2 && b.IdStudio == studioId &&
                    b.StartDatetime < end && b.EndDatetime > start);

            if (studioBlock)
                throw new InvalidOperationException(
                    "The studio is blocked during this time window.");

            bool coachBlock = await _context.BlockedPeriods
                .AnyAsync(b =>
                    b.Scope == 3 && b.IdCoach == coachId &&
                    b.StartDatetime < end && b.EndDatetime > start);

            if (coachBlock)
                throw new InvalidOperationException(
                    "The coach is unavailable during this time window.");
        }

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
                ClassOrigin = (ClassOrigin)c.ClassOrigin,
                StartDatetime = c.StartDatetime,
                EndDatetime = c.EndDatetime,
                ModalityId = c.IdModality,
                ModalityName = c.IdModalityNavigation.Name,
                StudioName = c.IdStudioNavigation.Name,
                CoachName = ResolveCoachName(c.IdCoachNavigation),
                MaxParticipants = c.MaxParticipants,
                CurrentParticipants = c.Participants.Count,
                CreatedAt = c.CreatedAt,
                StudentNames = c.Participants
                    .Where(p => p.ParentEnrollmentStatus != (byte)ParentEnrollmentStatus.Rejected)
                    .Select(p => ResolveStudentName(p.IdStudentNavigation))
                    .ToList()
            };

        private static string ResolveCoachName(Coach coach)
        {
            var person = coach.CoachNavigation?.PersonInfo;
            return person is not null
                ? $"{person.FirstName} {person.LastName}".Trim()
                : coach.CoachNavigation?.Username ?? $"Coach {coach.CoachId}";
        }

        internal static string ResolveStudentName(Student student)
        {
            var person = student.PersonInfo;
            return person is not null
                ? $"{person.FirstName} {person.LastName}".Trim()
                : $"Student {student.StudentId}";
        }

        private async Task<decimal> ComputeDefaultPriceAsync(DateTime classStart)
        {
            bool isSundayOrHoliday = classStart.DayOfWeek == DayOfWeek.Sunday;
            decimal weekdayRate = await _appSettingService.GetDecimalAsync("class_price_weekday", 36.00m);
            decimal weekendRate = await _appSettingService.GetDecimalAsync("class_price_weekend", 43.50m);
            return isSundayOrHoliday ? weekendRate : weekdayRate;
        }
    }
}
