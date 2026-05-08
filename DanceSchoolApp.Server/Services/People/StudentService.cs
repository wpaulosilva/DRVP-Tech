using DanceSchoolApp.Server.Data;
using DanceSchoolApp.Server.DTOs.Classes;
using DanceSchoolApp.Server.DTOs.People;
using DanceSchoolApp.Server.DTOs.Social;
using DanceSchoolApp.Server.Models;
using DanceSchoolApp.Server.Services.Social;
using Microsoft.EntityFrameworkCore;


namespace DanceSchoolApp.Server.Services.People
{
    public class StudentService
    {
        private readonly AppDbContext _context;
        private readonly NotificationService _notificationService;

        public StudentService(AppDbContext context, NotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
        }


        //  Queries 

        public async Task<List<StudentListResponse>> GetStudentsAsync()
        {
            return await _context.Students
                .Include(s => s.PersonInfo)
                .Select(s => new StudentListResponse
                {
                    StudentId = s.StudentId,
                    ParentUserId = s.ParentUserId,
                    IsActive = s.IsActive,
                    AcceptanceStatus = (StudentAcceptanceStatus)s.AcceptanceStatus,
                    PersonInfo = s.PersonInfo == null ? null : new PersonListResponse
                    {
                        PersonId = s.PersonInfo.PersonId,
                        FirstName = s.PersonInfo.FirstName,
                        LastName = s.PersonInfo.LastName
                    }
                })
                .ToListAsync();
        }

        public async Task<StudentDetailResponse> GetStudentAsync(int id)
        {
            var student = await _context.Students
                .Include(s => s.PersonInfo)
                .FirstOrDefaultAsync(s => s.StudentId == id);

            if (student is null)
                throw new KeyNotFoundException($"Student with id {id} was not found.");

            return new StudentDetailResponse
            {
                StudentId = student.StudentId,
                ParentUserId = student.ParentUserId,
                IsActive = student.IsActive,
                AcceptanceStatus = (StudentAcceptanceStatus)student.AcceptanceStatus,
                PersonInfo = student.PersonInfo is null ? null : new PersonDetailResponse
                {
                    PersonId = student.PersonInfo.PersonId,
                    FirstName = student.PersonInfo.FirstName,
                    LastName = student.PersonInfo.LastName,
                    BirthDate = student.PersonInfo.BirthDate,
                    Phone = student.PersonInfo.Phone,
                    Address = student.PersonInfo.Address,
                    Nif = student.PersonInfo.Nif
                }
            };
        }

        public async Task<List<StudentListResponse>> GetStudentsByParentAsync(int parentId)
        {
            bool parentExists = await _context.Users
                .AnyAsync(u => u.UserId == parentId);

            if (!parentExists)
                throw new KeyNotFoundException($"User with id {parentId} was not found.");

            return await _context.Students
                .Include(s => s.PersonInfo)
                .Where(s => s.ParentUserId == parentId)
                .Select(s => new StudentListResponse
                {
                    StudentId = s.StudentId,
                    ParentUserId = s.ParentUserId,
                    IsActive = s.IsActive,
                    AcceptanceStatus = (StudentAcceptanceStatus)s.AcceptanceStatus,
                    PersonInfo = s.PersonInfo == null ? null : new PersonListResponse
                    {
                        PersonId = s.PersonInfo.PersonId,
                        FirstName = s.PersonInfo.FirstName,
                        LastName = s.PersonInfo.LastName
                    }
                })
                .ToListAsync();
        }

        //  Commands 

        public async Task<int> CreateStudentAsync(StudentCreateRequest request)
        {
            bool parentExists = await _context.Users
                .AnyAsync(u => u.UserId == request.ParentId);

            if (!parentExists)
                throw new KeyNotFoundException($"User with id {request.ParentId} was not found.");

            if (request.Nif is not null)
                await EnsureNifUniqueAsync(request.Nif);

            // Validate birth date bounds
            if (request.BirthDate is not null)
                ValidateBirthDate(request.BirthDate.Value);

            var student = new Student
            {
                ParentUserId = request.ParentId,
                IsActive = true,
                PersonInfo = new PersonInfo
                {
                    FirstName = request.FirstName,
                    LastName = request.LastName,
                    BirthDate = request.BirthDate ?? default,
                    Phone = request.Phone,
                    Address = request.Address,
                    Nif = request.Nif
                }
            };

            _context.Students.Add(student);
            await _context.SaveChangesAsync();

            return student.StudentId;
        }

        public async Task UpdateStudentAsync(int id, StudentUpdateRequest request)
        {
            var student = await _context.Students
                .Include(s => s.PersonInfo)
                .FirstOrDefaultAsync(s => s.StudentId == id);

            if (student is null)
                throw new KeyNotFoundException($"Student with id {id} was not found.");

            if (student.PersonInfo is null)
                throw new InvalidOperationException($"Student with id {id} has no personal info record.");

            if (request.Nif is not null)
                await EnsureNifUniqueAsync(request.Nif, student.PersonInfo.PersonId);

            student.PersonInfo.FirstName = request.FirstName;
            student.PersonInfo.LastName = request.LastName;
            if (request.BirthDate is not null)
                ValidateBirthDate(request.BirthDate.Value);
            student.PersonInfo.BirthDate = request.BirthDate ?? default;
            student.PersonInfo.Phone = request.Phone;
            student.PersonInfo.Address = request.Address;
            student.PersonInfo.Nif = request.Nif;

            // When parent corrects data, reset to Pending so staff re-reviews
            student.AcceptanceStatus = (byte)StudentAcceptanceStatus.Pending;

            await _context.SaveChangesAsync();
        }

        // Updates PersonInfo only — does NOT reset AcceptanceStatus.
        public async Task UpdatePersonInfoAsync(int studentId, StudentUpdateRequest request)
        {
            var student = await _context.Students
                .Include(s => s.PersonInfo)
                .FirstOrDefaultAsync(s => s.StudentId == studentId);

            if (student is null)
                throw new KeyNotFoundException($"Student with id {studentId} was not found.");

            if (student.PersonInfo is null)
                throw new InvalidOperationException(
                    $"Student with id {studentId} has no personal info record.");

            if (request.Nif is not null)
                await EnsureNifUniqueAsync(request.Nif, student.PersonInfo.PersonId);

            student.PersonInfo.FirstName = request.FirstName;
            student.PersonInfo.LastName  = request.LastName;
            if (request.BirthDate is not null)
                ValidateBirthDate(request.BirthDate.Value);
            student.PersonInfo.BirthDate = request.BirthDate ?? default;
            student.PersonInfo.Phone     = request.Phone;
            student.PersonInfo.Address   = request.Address;
            student.PersonInfo.Nif       = request.Nif;

            await _context.SaveChangesAsync();
        }

        public async Task SetStudentStateAsync(int studentId, bool isActive)
        {
            var rowsAffected = await _context.Students
                .Where(s => s.StudentId == studentId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, isActive));

            if (rowsAffected == 0)
                throw new KeyNotFoundException($"Student with id {studentId} was not found.");
        }

        public async Task AcceptStudentAsync(int studentId)
        {
            var student = await _context.Students
                .FirstOrDefaultAsync(s => s.StudentId == studentId);

            if (student is null)
                throw new KeyNotFoundException($"Student with id {studentId} was not found.");

            if (student.AcceptanceStatus == (byte)StudentAcceptanceStatus.Accepted)
                throw new InvalidOperationException("Student is already accepted.");

            student.AcceptanceStatus = (byte)StudentAcceptanceStatus.Accepted;
            await _context.SaveChangesAsync();

            await _notificationService.SendAsync(
                userId: student.ParentUserId,
                title: "Aluno Admitido",
                message: $"O seu educando foi aceite e já pode participar nas aulas.",
                type: NotificationType.Success,
                entityType: "Student",
                entityId: studentId);
        }

        public async Task RejectStudentAsync(int studentId, string? reason)
        {
            var student = await _context.Students
                .Include(s => s.ParentUser)
                .FirstOrDefaultAsync(s => s.StudentId == studentId);

            if (student is null)
                throw new KeyNotFoundException($"Student with id {studentId} was not found.");

            student.AcceptanceStatus = (byte)StudentAcceptanceStatus.Rejected;
            await _context.SaveChangesAsync();

            await _notificationService.SendAsync(
                userId: student.ParentUserId,
                title: "Dados do Aluno Requerem Correção",
                message: reason is not null
                    ? $"O registo do seu educando não foi aceite. Motivo: {reason}. Por favor, atualize as informações do aluno."
                    : "O registo do seu educando não foi aceite. Por favor, reveja e atualize as informações do aluno.",
                type: NotificationType.Warning,
                entityType: "Student",
                entityId: studentId);
        }

        public async Task<List<StudentClassHistoryResponse>> GetStudentClassesAsync(int studentId)
        {
            bool studentExists = await _context.Students
                .AnyAsync(s => s.StudentId == studentId);

            if (!studentExists)
                throw new KeyNotFoundException($"Student with id {studentId} was not found.");

            var participants = await _context.Participants
                .Include(p => p.IdCoachClassNavigation)
                    .ThenInclude(c => c.IdModalityNavigation)
                .Include(p => p.IdCoachClassNavigation)
                    .ThenInclude(c => c.IdStudioNavigation)
                .Include(p => p.IdCoachClassNavigation)
                    .ThenInclude(c => c.IdCoachNavigation)
                        .ThenInclude(coach => coach.CoachNavigation)
                            .ThenInclude(u => u.PersonInfo)
                .Where(p => p.IdStudent == studentId)
                .OrderByDescending(p => p.IdCoachClassNavigation.StartDatetime)
                .ToListAsync();

            return participants.Select(p => new StudentClassHistoryResponse
            {
                ParticipantId     = p.ParticipantId,
                ClassId           = p.IdCoachClass,
                Status            = (CoachClassStatus)p.IdCoachClassNavigation.Status,
                StartDatetime     = p.IdCoachClassNavigation.StartDatetime,
                EndDatetime       = p.IdCoachClassNavigation.EndDatetime,
                ModalityName      = p.IdCoachClassNavigation.IdModalityNavigation.Name,
                StudioName        = p.IdCoachClassNavigation.IdStudioNavigation.Name,
                CoachName         = ResolveCoachName(p.IdCoachClassNavigation.IdCoachNavigation),
                JoinedAt          = p.JoinedAt,
                ValidationStatus  = (ParticipantValidationStatus)p.ValidationStatus,
                ParentValidatedAt = p.ParentValidatedAt
            }).ToList();
        }

        //  Private helpers 

        private static void ValidateBirthDate(DateOnly birthDate)
        {
            var today = DateOnly.FromDateTime(DateTime.Now);
            if (birthDate > today)
                throw new InvalidOperationException("BirthDate cannot be in the future.");

            var hundredYearsAgo = today.AddYears(-100);
            if (birthDate < hundredYearsAgo)
                throw new InvalidOperationException("BirthDate is too far in the past.");
        }

        private async Task EnsureNifUniqueAsync(string nif, int? excludePersonId = null)
        {
            var query = _context.PersonInfos
                .Where(p => p.Nif == nif);

            if (excludePersonId.HasValue)
                query = query.Where(p => p.PersonId != excludePersonId.Value);

            if (await query.AnyAsync())
                throw new InvalidOperationException($"NIF '{nif}' is already in use.");
        }

        private static string ResolveCoachName(Coach coach)
        {
            var person = coach.CoachNavigation?.PersonInfo;
            return person is not null
                ? $"{person.FirstName} {person.LastName}".Trim()
                : coach.CoachNavigation?.Username ?? $"Coach {coach.CoachId}";
        }

    }
}
