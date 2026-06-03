using DanceSchoolApp.Server.Data;
using DanceSchoolApp.Server.DTOs;
using DanceSchoolApp.Server.DTOs.Classes;
using DanceSchoolApp.Server.DTOs.Social;
using DanceSchoolApp.Server.Models;
using DanceSchoolApp.Server.Services.Social;
using Microsoft.EntityFrameworkCore;

namespace DanceSchoolApp.Server.Services.Classes
{
    public class ParticipantService
    {
        private readonly AppDbContext _context;
        private readonly NotificationService _notificationService;
        private readonly AppSettingService _appSettingService;

        public ParticipantService(AppDbContext context,
            NotificationService notificationService,
            AppSettingService appSettingService)
        {
            _context = context;
            _notificationService = notificationService;
            _appSettingService = appSettingService;
        }

        private async Task<decimal> ComputeDefaultPriceAsync(DateTime classStart)
        {
            bool isSundayOrHoliday = classStart.DayOfWeek == DayOfWeek.Sunday;
            decimal weekdayRate = await _appSettingService.GetDecimalAsync("class_price_weekday", 36.00m);
            decimal weekendRate = await _appSettingService.GetDecimalAsync("class_price_weekend", 43.50m);
            return isSundayOrHoliday ? weekendRate : weekdayRate;
        }

        //  Queries

        public async Task<PagedResult<ParticipantListResponse>> GetByClassAsync(int classId, PagedQuery query)
        {
            bool classExists = await _context.CoachClasses
                .AnyAsync(c => c.ClassId == classId);

            if (!classExists)
                throw new KeyNotFoundException($"Class with id {classId} was not found.");

            var dbQuery = _context.Participants
                .Include(p => p.IdStudentNavigation)
                    .ThenInclude(s => s.PersonInfo)
                .Where(p => p.IdCoachClass == classId);

            var total = await dbQuery.CountAsync();

            var items = await dbQuery
                .OrderBy(p => p.JoinedAt)
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .Select(p => new ParticipantListResponse
                {
                    ParticipantId          = p.ParticipantId,
                    ClassId                = p.IdCoachClass,
                    StudentId              = p.IdStudent,
                    StudentName            = p.IdStudentNavigation.PersonInfo != null
                        ? (p.IdStudentNavigation.PersonInfo.FirstName + " " +
                           p.IdStudentNavigation.PersonInfo.LastName).Trim()
                        : "Student " + p.IdStudent.ToString(),
                    ParentUserId           = p.IdStudentNavigation.ParentUserId,
                    JoinedAt               = p.JoinedAt,
                    ValidationStatus       = (ParticipantValidationStatus)p.ValidationStatus,
                    ParentValidatedAt      = p.ParentValidatedAt,
                    ParentEnrollmentStatus = (ParentEnrollmentStatus)p.ParentEnrollmentStatus,
                    ParentEnrollmentAt     = p.ParentEnrollmentAt
                })
                .ToListAsync();

            return new PagedResult<ParticipantListResponse>
            {
                Items      = items,
                TotalCount = total,
                Page       = query.Page,
                PageSize   = query.PageSize
            };
        }

        //  Commands

        // Parent joins an already-Approved open class with one of their students.
        public async Task<int> JoinClassAsync(ParticipantJoinRequest request, int callingUserId)
        {
            var coachClass = await _context.CoachClasses
                .Include(c => c.Participants)
                .FirstOrDefaultAsync(c => c.ClassId == request.ClassId);

            if (coachClass is null)
                throw new KeyNotFoundException(
                    $"Class with id {request.ClassId} was not found.");

            if (coachClass.Status != (byte)CoachClassStatus.Approved)
                throw new InvalidOperationException(
                    "Students can only join classes with Approved status.");

            if (coachClass.Participants.Count >= coachClass.MaxParticipants)
                throw new InvalidOperationException(
                    "This class is full. No spots available.");

            var student = await _context.Students
                .Include(s => s.IdModalities)
                .FirstOrDefaultAsync(s => s.StudentId == request.StudentId && s.IsActive);

            if (student is null)
                throw new KeyNotFoundException(
                    $"Student with id {request.StudentId} was not found or is inactive.");

            if (student.ParentUserId != callingUserId)
                throw new UnauthorizedAccessException("You can only enroll your own students.");

            if (student.AcceptanceStatus != 1)
                throw new InvalidOperationException(
                    "This student has not been accepted by staff yet and cannot join classes.");

            if (!student.IdModalities.Any(m => m.ModalityId == coachClass.IdModality))
                throw new InvalidOperationException(
                    $"Student {request.StudentId} is not assigned to the modality of this class.");

            if (coachClass.Participants.Any(p => p.IdStudent == request.StudentId))
                throw new InvalidOperationException(
                    $"Student {request.StudentId} is already enrolled in this class.");

            bool timeConflict = await _context.Participants
                .Include(p => p.IdCoachClassNavigation)
                .AnyAsync(p =>
                    p.IdStudent == request.StudentId &&
                    p.ParentEnrollmentStatus != (byte)ParentEnrollmentStatus.Rejected &&
                    p.IdCoachClassNavigation.Status != (byte)CoachClassStatus.Rejected &&
                    p.IdCoachClassNavigation.Status != (byte)CoachClassStatus.Cancelled &&
                    p.IdCoachClassNavigation.StartDatetime < coachClass.EndDatetime &&
                    p.IdCoachClassNavigation.EndDatetime > coachClass.StartDatetime);

            if (timeConflict)
                throw new InvalidOperationException(
                    "This student is already enrolled in another class at this time.");

            var participant = new Participant
            {
                IdCoachClass           = request.ClassId,
                IdStudent              = request.StudentId,
                JoinedAt               = DateOnly.FromDateTime(DateTime.Now),
                ValidationStatus       = (byte)ParticipantValidationStatus.Pending,
                ParentEnrollmentStatus = (byte)ParentEnrollmentStatus.NotRequired,
                PerParticipantPrice    = await ComputeDefaultPriceAsync(coachClass.StartDatetime)
            };

            _context.Participants.Add(participant);
            await _context.SaveChangesAsync();

            await _notificationService.SendAsync(
                userId: coachClass.IdCoach,
                title: "Novo aluno inscrito",
                message: $"Um aluno inscreveu-se na sua aula em {coachClass.StartDatetime:dd/MM/yyyy HH:mm}.",
                type: NotificationType.ClassUpdate,
                entityType: "CoachClass",
                entityId: request.ClassId);

            return participant.ParticipantId;
        }

        // Parent joins a class they were invited to (Requested, CoachApproved, or Approved).
        public async Task<int> InviteJoinAsync(ParticipantJoinRequest request, int callingUserId)
        {
            var coachClass = await _context.CoachClasses
                .Include(c => c.Participants)
                .FirstOrDefaultAsync(c => c.ClassId == request.ClassId);

            if (coachClass is null)
                throw new KeyNotFoundException(
                    $"Class with id {request.ClassId} was not found.");

            var allowedStatuses = new byte[]
            {
                (byte)CoachClassStatus.Requested,
                (byte)CoachClassStatus.CoachApproved,
                (byte)CoachClassStatus.Approved
            };

            if (!allowedStatuses.Contains(coachClass.Status))
                throw new InvalidOperationException(
                    "You can only join a class that is in Requested, CoachApproved, or Approved status.");

            if (coachClass.Participants.Count >= coachClass.MaxParticipants)
                throw new InvalidOperationException(
                    "This class is full. No spots available.");

            var student = await _context.Students
                .Include(s => s.IdModalities)
                .FirstOrDefaultAsync(s => s.StudentId == request.StudentId && s.IsActive);

            if (student is null)
                throw new KeyNotFoundException(
                    $"Student with id {request.StudentId} was not found or is inactive.");

            if (student.ParentUserId != callingUserId)
                throw new UnauthorizedAccessException("You can only enroll your own students.");

            if (student.AcceptanceStatus != 1)
                throw new InvalidOperationException(
                    "This student has not been accepted by staff yet and cannot join classes.");

            if (!student.IdModalities.Any(m => m.ModalityId == coachClass.IdModality))
                throw new InvalidOperationException(
                    $"Student {request.StudentId} is not assigned to the modality of this class.");

            if (coachClass.Participants.Any(p => p.IdStudent == request.StudentId))
                throw new InvalidOperationException(
                    $"Student {request.StudentId} is already enrolled in this class.");

            bool timeConflict = await _context.Participants
                .Include(p => p.IdCoachClassNavigation)
                .AnyAsync(p =>
                    p.IdStudent == request.StudentId &&
                    p.ParentEnrollmentStatus != (byte)ParentEnrollmentStatus.Rejected &&
                    p.IdCoachClassNavigation.Status != (byte)CoachClassStatus.Rejected &&
                    p.IdCoachClassNavigation.Status != (byte)CoachClassStatus.Cancelled &&
                    p.IdCoachClassNavigation.StartDatetime < coachClass.EndDatetime &&
                    p.IdCoachClassNavigation.EndDatetime > coachClass.StartDatetime);

            if (timeConflict)
                throw new InvalidOperationException(
                    "This student is already enrolled in another class at this time.");

            var participant = new Participant
            {
                IdCoachClass           = request.ClassId,
                IdStudent              = request.StudentId,
                JoinedAt               = DateOnly.FromDateTime(DateTime.Now),
                ValidationStatus       = (byte)ParticipantValidationStatus.Pending,
                ParentEnrollmentStatus = (byte)ParentEnrollmentStatus.NotRequired,
                PerParticipantPrice    = await ComputeDefaultPriceAsync(coachClass.StartDatetime)
            };

            _context.Participants.Add(participant);
            await _context.SaveChangesAsync();

            await _notificationService.SendAsync(
                userId: coachClass.IdCoach,
                title: "Novo aluno inscrito",
                message: $"Um aluno inscreveu-se na sua aula em {coachClass.StartDatetime:dd/MM/yyyy HH:mm}.",
                type: NotificationType.ClassUpdate,
                entityType: "CoachClass",
                entityId: request.ClassId);

            return participant.ParticipantId;
        }

        // Parent approves or rejects their student's enrollment in a coach-created class.
        // Called while the class is still in Requested status.
        // When all parents have responded: auto-advances to CoachApproved (if ≥1 approved)
        // or auto-cancels (if all rejected).
        public async Task ParentApproveEnrollmentAsync(int participantId, bool approve, int callingUserId)
        {
            var participant = await _context.Participants
                .Include(p => p.IdCoachClassNavigation)
                .Include(p => p.IdStudentNavigation)
                    .ThenInclude(s => s.PersonInfo)
                .FirstOrDefaultAsync(p => p.ParticipantId == participantId);

            if (participant is null)
                throw new KeyNotFoundException(
                    $"Participant record with id {participantId} was not found.");

            if (participant.IdStudentNavigation.ParentUserId != callingUserId)
                throw new UnauthorizedAccessException(
                    "You can only respond to enrollment requests for your own students.");

            if (participant.IdCoachClassNavigation.ClassOrigin != (byte)ClassOrigin.CoachCreated)
                throw new InvalidOperationException(
                    "Enrollment approval is only required for coach-created classes.");

            if (participant.IdCoachClassNavigation.Status != (byte)CoachClassStatus.Requested)
                throw new InvalidOperationException(
                    "Enrollment approval is only available while the class is in Requested status.");

            if (participant.ParentEnrollmentStatus != (byte)ParentEnrollmentStatus.Pending)
                throw new InvalidOperationException(
                    "This enrollment request has already been responded to.");

            participant.ParentEnrollmentStatus = approve
                ? (byte)ParentEnrollmentStatus.Approved
                : (byte)ParentEnrollmentStatus.Rejected;
            participant.ParentEnrollmentAt = DateTime.Now;

            await _context.SaveChangesAsync();

            // Notify coach of parent's decision
            var coachClass = participant.IdCoachClassNavigation;
            var person     = participant.IdStudentNavigation.PersonInfo;
            var studentName = person is not null
                ? $"{person.FirstName} {person.LastName}".Trim()
                : $"Student {participant.IdStudent}";

            await _notificationService.SendAsync(
                userId: coachClass.IdCoach,
                title: approve ? "Aluno confirmou inscrição" : "Aluno recusou inscrição",
                message: approve
                    ? $"{studentName} confirmou a inscrição na aula de {coachClass.StartDatetime:dd/MM/yyyy HH:mm}."
                    : $"{studentName} recusou a inscrição na aula de {coachClass.StartDatetime:dd/MM/yyyy HH:mm}.",
                type: approve ? NotificationType.Success : NotificationType.Warning,
                entityType: "CoachClass",
                entityId: coachClass.ClassId);

            await TryAdvanceFromEnrollmentAsync(coachClass.ClassId);
        }

        // Post-class attendance confirmation by parent.
        public async Task ParentValidateAsync(int participantId, bool attended, int callingUserId)
        {
            var participant = await _context.Participants
                .Include(p => p.IdCoachClassNavigation)
                .Include(p => p.IdStudentNavigation)
                .FirstOrDefaultAsync(p => p.ParticipantId == participantId);

            if (participant is null)
                throw new KeyNotFoundException(
                    $"Participant record with id {participantId} was not found.");

            if (participant.IdStudentNavigation.ParentUserId != callingUserId)
                throw new UnauthorizedAccessException(
                    "You can only validate attendance for your own students.");

            var status = participant.IdCoachClassNavigation.Status;
            if (status != (byte)CoachClassStatus.Finished)
                throw new InvalidOperationException(
                    "Validation is only available for Finished classes.");

            if (participant.IdCoachClassNavigation.CoachValidatedAt is null)
                throw new InvalidOperationException(
                    "Validation is only available after coach validates.");

            if (participant.ValidationStatus != (byte)ParticipantValidationStatus.Pending)
                throw new InvalidOperationException(
                    "This participant record has already been validated.");

            participant.ValidationStatus = attended
                ? (byte)ParticipantValidationStatus.ParentConfirmed
                : (byte)ParticipantValidationStatus.Disputed;

            participant.ParentValidatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            await TryAdvanceClassToStaffReviewAsync(participant.IdCoachClass);
        }

        public async Task RemoveParticipantAsync(int participantId)
        {
            var participant = await _context.Participants
                .Include(p => p.IdCoachClassNavigation)
                .FirstOrDefaultAsync(p => p.ParticipantId == participantId);

            if (participant is null)
                throw new KeyNotFoundException(
                    $"Participant record with id {participantId} was not found.");

            var safeStatuses = new byte[]
            {
                (byte)CoachClassStatus.Requested,
                (byte)CoachClassStatus.Approved
            };

            if (!safeStatuses.Contains(participant.IdCoachClassNavigation.Status))
                throw new InvalidOperationException(
                    "A participant can only be removed from a Requested or Approved class. " +
                    "The class has already progressed past that point.");

            int participantCount = await _context.Participants
                .CountAsync(p => p.IdCoachClass == participant.IdCoachClass);

            if (participantCount <= 1)
                throw new InvalidOperationException(
                    "Cannot remove the last participant from a class. Cancel the class instead.");

            _context.Participants.Remove(participant);
            await _context.SaveChangesAsync();
        }

        //  Private helpers

        // Called after each parent enrollment response.
        // If no participants are still Pending → decide whether to advance or cancel the class.
        private async Task TryAdvanceFromEnrollmentAsync(int classId)
        {
            var allParticipants = await _context.Participants
                .Where(p => p.IdCoachClass == classId)
                .ToListAsync();

            bool anyPending = allParticipants.Any(
                p => p.ParentEnrollmentStatus == (byte)ParentEnrollmentStatus.Pending);

            if (anyPending) return;

            var coachClass = await _context.CoachClasses
                .FirstOrDefaultAsync(c => c.ClassId == classId);

            if (coachClass is null || coachClass.Status != (byte)CoachClassStatus.Requested)
                return;

            int approvedCount = allParticipants.Count(
                p => p.ParentEnrollmentStatus == (byte)ParentEnrollmentStatus.Approved);

            if (approvedCount > 0)
            {
                coachClass.Status = (byte)CoachClassStatus.CoachApproved;
                await _context.SaveChangesAsync();

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
                        title: "Aula aguarda aprovação do staff",
                        message: $"Todos os pais responderam à inscrição da aula de {scheduledAt}. Por favor, aprove ou rejeite.",
                        type: NotificationType.Success,
                        entityType: "CoachClass",
                        entityId: classId);
                }
            }
            else
            {
                // All rejected — auto-cancel
                coachClass.Status = (byte)CoachClassStatus.Cancelled;
                await _context.SaveChangesAsync();

                await _notificationService.SendAsync(
                    userId: coachClass.IdCoach,
                    title: "Aula cancelada automaticamente",
                    message: $"A aula de {coachClass.StartDatetime:dd/MM/yyyy HH:mm} foi cancelada porque todos os pais recusaram a inscrição.",
                    type: NotificationType.Warning,
                    entityType: "CoachClass",
                    entityId: classId);
            }
        }

        // Called after each parent attendance validation.
        // Advances to Pending when all active participants AND coach have responded.
        private async Task TryAdvanceClassToStaffReviewAsync(int classId)
        {
            var allParticipants = await _context.Participants
                .Where(p => p.IdCoachClass == classId)
                .ToListAsync();

            // Only count participants who are actively enrolled (not enrollment-rejected)
            var activeParticipants = allParticipants
                .Where(p => p.ParentEnrollmentStatus != (byte)ParentEnrollmentStatus.Rejected)
                .ToList();

            bool allResponded = activeParticipants
                .All(p => p.ValidationStatus != (byte)ParticipantValidationStatus.Pending);

            if (!allResponded) return;

            var coachClass = await _context.CoachClasses
                .FirstOrDefaultAsync(c => c.ClassId == classId);

            if (coachClass is null) return;
            if (coachClass.Status != (byte)CoachClassStatus.Finished) return;
            if (coachClass.CoachValidationStatus == (byte)CoachValidationStatus.Pending) return;

            coachClass.Status = (byte)CoachClassStatus.Pending;
            await _context.SaveChangesAsync();
        }
    }
}
