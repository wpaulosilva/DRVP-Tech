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

        public ParticipantService(AppDbContext context,
            NotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
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
                    ParticipantId     = p.ParticipantId,
                    ClassId           = p.IdCoachClass,
                    StudentId         = p.IdStudent,
                    StudentName       = p.IdStudentNavigation.PersonInfo != null
                        ? (p.IdStudentNavigation.PersonInfo.FirstName + " " +
                           p.IdStudentNavigation.PersonInfo.LastName).Trim()
                        : "Student " + p.IdStudent.ToString(),
                    ParentUserId      = p.IdStudentNavigation.ParentUserId,
                    JoinedAt          = p.JoinedAt,
                    ValidationStatus  = (ParticipantValidationStatus)p.ValidationStatus,
                    ParentValidatedAt = p.ParentValidatedAt
                })
                .ToListAsync();

            return new PagedResult<ParticipantListResponse>
            {
                Items     = items,
                TotalCount = total,
                Page      = query.Page,
                PageSize  = query.PageSize
            };
        }

        //  Commands 

        public async Task<int> JoinClassAsync(ParticipantJoinRequest request, int callingUserId)
        {
            //  1. Class must exist and be open (Approved + has space) 
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

            //  2. Student must exist and be active 
            var student = await _context.Students
                .FirstOrDefaultAsync(s => s.StudentId == request.StudentId
                                       && s.IsActive);

            if (student is null)
                throw new KeyNotFoundException(
                    $"Student with id {request.StudentId} was not found or is inactive.");

            if (student.ParentUserId != callingUserId)
                throw new UnauthorizedAccessException("You can only enroll your own students.");

            if (student.AcceptanceStatus != 1)
                throw new InvalidOperationException(
                    "This student has not been accepted by staff yet and cannot join classes.");

            //  3. Student must belong to the requesting parent 

            //  4. Student not already enrolled in this class 
            // The DB has a unique constraint UQ_ClassStudent, but we catch it
            // here for a clean error message rather than a constraint exception.
            bool alreadyEnrolled = coachClass.Participants
                .Any(p => p.IdStudent == request.StudentId);

            if (alreadyEnrolled)
                throw new InvalidOperationException(
                    $"Student {request.StudentId} is already enrolled in this class.");

            //  5. Student not already in another class at the same time 
            bool timeConflict = await _context.Participants
                .Include(p => p.IdCoachClassNavigation)
                .AnyAsync(p =>
                    p.IdStudent == request.StudentId &&
                    p.IdCoachClassNavigation.Status != (byte)CoachClassStatus.Rejected &&
                    p.IdCoachClassNavigation.Status != (byte)CoachClassStatus.Cancelled &&
                    p.IdCoachClassNavigation.StartDatetime < coachClass.EndDatetime &&
                    p.IdCoachClassNavigation.EndDatetime > coachClass.StartDatetime);

            if (timeConflict)
                throw new InvalidOperationException(
                    "This student is already enrolled in another class at this time.");

            //  6. Enroll 
            var participant = new Participant
            {
                IdCoachClass = request.ClassId,
                IdStudent = request.StudentId,
                JoinedAt = DateOnly.FromDateTime(DateTime.Now),
                ValidationStatus = (byte)ParticipantValidationStatus.Pending
            };

            _context.Participants.Add(participant);
            await _context.SaveChangesAsync();

            // Notify coach that a new student joined
            var classInfo = await _context.CoachClasses
                .FirstOrDefaultAsync(c => c.ClassId == request.ClassId);

            if (classInfo is not null)
            {
                await _notificationService.SendAsync(
                    userId: classInfo.IdCoach,
                    title: "Novo aluno inscrito",
                    message: $"Um aluno inscreveu-se na sua aula em {classInfo.StartDatetime:dd/MM/yyyy HH:mm}.",
                    type: NotificationType.ClassUpdate,
                    entityType: "CoachClass",
                    entityId: request.ClassId);
            }

            return participant.ParticipantId;
        }

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

            // Validation only makes sense after the class is finished
            var status = participant.IdCoachClassNavigation.Status;
            if (status != (byte)CoachClassStatus.Finished)
                throw new InvalidOperationException(
                    "Validation is only available for Finished classes.");

            // Parent can only validate after the coach validates first.
            if (participant.IdCoachClassNavigation.CoachValidatedAt is null)
                throw new InvalidOperationException(
                    "Validation is only available after coach validates.");

            // Prevent re-validation once already responded.
            if (participant.ValidationStatus != (byte)ParticipantValidationStatus.Pending)
                throw new InvalidOperationException(
                    "This participant record has already been validated.");

            participant.ValidationStatus = attended
                ? (byte)ParticipantValidationStatus.ParentConfirmed
                : (byte)ParticipantValidationStatus.Disputed;

            participant.ParentValidatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            // After saving, check if all participants in this class have now
            // responded — if so, the class can be moved to Pending for staff
            // review. Staff will then do the final sign-off (Validated).
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

            // Removing a participant from a Finished/Validated/Pending class
            // would corrupt billing records — block it.
            var safeStatuses = new byte[]
            {
                (byte)CoachClassStatus.Requested,
                (byte)CoachClassStatus.Approved
            };

            if (!safeStatuses.Contains(participant.IdCoachClassNavigation.Status))
                throw new InvalidOperationException(
                    "A participant can only be removed from a Requested or Approved class. " +
                    "The class has already progressed past that point.");

            // Guard: if this is the last participant, block the removal.
            // A class with zero students should not exist — cancel the class instead.
            int participantCount = await _context.Participants
                .CountAsync(p => p.IdCoachClass == participant.IdCoachClass);

            if (participantCount <= 1)
                throw new InvalidOperationException(
                    "Cannot remove the last participant from a class. " +
                    "Cancel the class instead.");

            _context.Participants.Remove(participant);
            await _context.SaveChangesAsync();
        }

        //  Private helpers 

        
 
        private async Task TryAdvanceClassToStaffReviewAsync(int classId)
        {
            var allParticipants = await _context.Participants
                .Where(p => p.IdCoachClass == classId)
                .ToListAsync();

            bool allResponded = allParticipants
                .All(p => p.ValidationStatus != (byte)ParticipantValidationStatus.Pending);

            if (!allResponded) return;

            var coachClass = await _context.CoachClasses
                .FirstOrDefaultAsync(c => c.ClassId == classId);

            if (coachClass is null) return;
            if (coachClass.Status != (byte)CoachClassStatus.Finished)
                return;

            if (coachClass.CoachValidationStatus == (byte)CoachValidationStatus.Pending)
                return;

            coachClass.Status = (byte)CoachClassStatus.Pending;
            await _context.SaveChangesAsync();
        }
    }
}
