using DanceSchoolApp.Server.Data;
using DanceSchoolApp.Server.DTOs.Classes;
using DanceSchoolApp.Server.Services;
using DanceSchoolApp.Server.Services.Classes;
using DanceSchoolApp.Server.Services.Social;
using DanceSchoolApp.Tests.Helpers;
using FluentAssertions;

namespace DanceSchoolApp.Tests.Unit;

/// <summary>
/// Tests for the coach-created class flow:
///   Coach creates → parents approve enrollment → auto-advances to CoachApproved (or auto-cancels).
/// </summary>
[Trait("Category", "Unit")]
public class CoachClassCoachCreateTests
{
    private static CoachClassService CreateClassService(AppDbContext db) =>
        new CoachClassService(db, new NotificationService(db), new AppSettingService(db));

    private static ParticipantService CreateParticipantService(AppDbContext db) =>
        new ParticipantService(db, new NotificationService(db), new AppSettingService(db));

    private static DateTime NextWeekdayAt(DayOfWeek target, int hour)
    {
        var today = DateTime.UtcNow.Date;
        var days  = ((int)target - (int)today.DayOfWeek + 7) % 7;
        if (days == 0) days = 7;
        return DateTime.SpecifyKind(today.AddDays(days).AddHours(hour), DateTimeKind.Utc);
    }

    private static (AppDbContext db, int coachUserId, int modalityId,
                    int parentUserId, int studentId,
                    DanceSchoolApp.Server.Models.Coach coach)
        SetupBase()
    {
        var db = DbContextFactory.Create();
        SeedData.SeedRoles(db);

        var modality = SeedData.SeedModality(db);
        var studio   = SeedData.SeedStudio(db);
        studio.IdModalities.Add(modality);
        db.SaveChanges();

        var coachUser = SeedData.SeedUserWithRole(db, "coachA", "coach");
        var coach     = SeedData.SeedCoach(db, coachUser);
        coach.IdModalities.Add(modality);
        db.SaveChanges();

        SeedData.SeedCoachAvailability(db, coach,
            weekday:   (byte)DayOfWeek.Monday,
            startTime: new TimeOnly(8, 0),
            endTime:   new TimeOnly(18, 0));

        var parentUser = SeedData.SeedUserWithRole(db, "parentA", "parent");
        var student    = SeedData.SeedStudent(db, parentUser);
        student.IdModalities.Add(modality);
        db.SaveChanges();

        return (db, coachUser.UserId, modality.ModalityId, parentUser.UserId, student.StudentId, coach);
    }

    //  Coach can create a class with students that have the modality

    [Fact]
    public async Task CoachCreateAsync_ValidRequest_ReturnsPositiveClassId()
    {
        var (db, coachUserId, modalityId, _, studentId, _) = SetupBase();
        var service = CreateClassService(db);
        var start = NextWeekdayAt(DayOfWeek.Monday, 10);

        var request = new CoachClassCoachCreateRequest
        {
            ModalityId      = modalityId,
            StartDatetime   = start,
            EndDatetime     = start.AddHours(1),
            MaxParticipants = 4,
            StudentIds      = new List<int> { studentId }
        };

        var classId = await service.CoachCreateAsync(request, coachUserId);

        classId.Should().BeGreaterThan(0);
    }

    //  Created class has ClassOrigin = CoachCreated and participants with Pending enrollment

    [Fact]
    public async Task CoachCreateAsync_CreatedClass_HasCorrectOriginAndEnrollmentStatus()
    {
        var (db, coachUserId, modalityId, _, studentId, _) = SetupBase();
        var service = CreateClassService(db);
        var start = NextWeekdayAt(DayOfWeek.Monday, 10);

        var request = new CoachClassCoachCreateRequest
        {
            ModalityId      = modalityId,
            StartDatetime   = start,
            EndDatetime     = start.AddHours(1),
            MaxParticipants = 4,
            StudentIds      = new List<int> { studentId }
        };

        var classId = await service.CoachCreateAsync(request, coachUserId);

        var coachClass = db.CoachClasses.Find(classId)!;
        coachClass.ClassOrigin.Should().Be((byte)ClassOrigin.CoachCreated);
        coachClass.Status.Should().Be((byte)CoachClassStatus.Requested);

        var participant = db.Participants.Single(p => p.IdCoachClass == classId);
        participant.ParentEnrollmentStatus.Should().Be((byte)ParentEnrollmentStatus.Pending);
    }

    //  Student not enrolled in the modality is rejected

    [Fact]
    public async Task CoachCreateAsync_StudentNotInModality_ThrowsInvalidOperation()
    {
        var (db, coachUserId, modalityId, parentUserId, _, _) = SetupBase();

        // Create a student without the modality
        var studentWithoutModality = SeedData.SeedStudent(db, db.Users.Find(parentUserId)!, "Xana");
        // deliberately do NOT assign the modality

        var service = CreateClassService(db);
        var start = NextWeekdayAt(DayOfWeek.Monday, 10);

        var request = new CoachClassCoachCreateRequest
        {
            ModalityId      = modalityId,
            StartDatetime   = start,
            EndDatetime     = start.AddHours(1),
            MaxParticipants = 4,
            StudentIds      = new List<int> { studentWithoutModality.StudentId }
        };

        Func<Task> act = () => service.CoachCreateAsync(request, coachUserId);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*modality*");
    }

    //  CoachRespondAsync is blocked for coach-created classes

    [Fact]
    public async Task CoachRespondAsync_OnCoachCreatedClass_ThrowsInvalidOperation()
    {
        var (db, coachUserId, modalityId, _, studentId, coach) = SetupBase();
        var modality = db.Modalities.Find(modalityId)!;
        var studio   = db.Studios.First();

        var parentUser = db.Users.Find(db.Students.Find(studentId)!.ParentUserId)!;
        var cls = SeedData.SeedCoachClass(db, coach, modality, studio, parentUser,
            status: (byte)CoachClassStatus.Requested,
            classOrigin: (byte)ClassOrigin.CoachCreated);

        var service = CreateClassService(db);

        Func<Task> act = () => service.CoachRespondAsync(cls.ClassId, coachUserId, accept: true, reason: null);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*coach-created*");
    }

    //  All parents approve → class auto-advances to CoachApproved

    [Fact]
    public async Task ParentApproveEnrollment_AllApprove_AdvancesToCoachApproved()
    {
        var (db, coachUserId, modalityId, parentUserId, studentId, _) = SetupBase();
        SeedData.SeedRoles(db); // ensure staff role exists for notification query

        var service    = CreateClassService(db);
        var partSvc    = CreateParticipantService(db);
        var start      = NextWeekdayAt(DayOfWeek.Monday, 10);

        var request = new CoachClassCoachCreateRequest
        {
            ModalityId      = modalityId,
            StartDatetime   = start,
            EndDatetime     = start.AddHours(1),
            MaxParticipants = 4,
            StudentIds      = new List<int> { studentId }
        };

        var classId = await service.CoachCreateAsync(request, coachUserId);
        var participant = db.Participants.Single(p => p.IdCoachClass == classId);

        await partSvc.ParentApproveEnrollmentAsync(participant.ParticipantId, approve: true, callingUserId: parentUserId);

        var updatedClass = db.CoachClasses.Find(classId)!;
        updatedClass.Status.Should().Be((byte)CoachClassStatus.CoachApproved,
            because: "when all parents approve, class should auto-advance to CoachApproved");

        db.Entry(participant).Reload();
        participant.ParentEnrollmentStatus.Should().Be((byte)ParentEnrollmentStatus.Approved);
        participant.ParentEnrollmentAt.Should().NotBeNull();
    }

    //  All parents reject → class auto-cancels

    [Fact]
    public async Task ParentApproveEnrollment_AllReject_AutoCancels()
    {
        var (db, coachUserId, modalityId, parentUserId, studentId, _) = SetupBase();

        var service = CreateClassService(db);
        var partSvc = CreateParticipantService(db);
        var start   = NextWeekdayAt(DayOfWeek.Monday, 10);

        var request = new CoachClassCoachCreateRequest
        {
            ModalityId      = modalityId,
            StartDatetime   = start,
            EndDatetime     = start.AddHours(1),
            MaxParticipants = 4,
            StudentIds      = new List<int> { studentId }
        };

        var classId    = await service.CoachCreateAsync(request, coachUserId);
        var participant = db.Participants.Single(p => p.IdCoachClass == classId);

        await partSvc.ParentApproveEnrollmentAsync(participant.ParticipantId, approve: false, callingUserId: parentUserId);

        var updatedClass = db.CoachClasses.Find(classId)!;
        updatedClass.Status.Should().Be((byte)CoachClassStatus.Cancelled,
            because: "when all parents reject, class should auto-cancel");
    }

    //  Partial approval — only one parent has responded, class stays Requested

    [Fact]
    public async Task ParentApproveEnrollment_OneOfTwoResponds_ClassStaysRequested()
    {
        var db       = DbContextFactory.Create();
        SeedData.SeedRoles(db);

        var modality = SeedData.SeedModality(db, "Salsa");
        var studio   = SeedData.SeedStudio(db, "Studio Z");
        studio.IdModalities.Add(modality);
        db.SaveChanges();

        var coachUser = SeedData.SeedUserWithRole(db, "coachB", "coach");
        var coach     = SeedData.SeedCoach(db, coachUser);
        coach.IdModalities.Add(modality);
        db.SaveChanges();

        SeedData.SeedCoachAvailability(db, coach,
            weekday: (byte)DayOfWeek.Monday,
            startTime: new TimeOnly(8, 0),
            endTime:   new TimeOnly(18, 0));

        var parentA = SeedData.SeedUserWithRole(db, "parentB1", "parent");
        var studentA = SeedData.SeedStudent(db, parentA, "Rita");
        studentA.IdModalities.Add(modality);

        var parentB = SeedData.SeedUserWithRole(db, "parentB2", "parent");
        var studentB = SeedData.SeedStudent(db, parentB, "Rui");
        studentB.IdModalities.Add(modality);

        db.SaveChanges();

        var service = CreateClassService(db);
        var partSvc = CreateParticipantService(db);
        var start   = NextWeekdayAt(DayOfWeek.Monday, 10);

        var request = new CoachClassCoachCreateRequest
        {
            ModalityId      = modality.ModalityId,
            StartDatetime   = start,
            EndDatetime     = start.AddHours(1),
            MaxParticipants = 4,
            StudentIds      = new List<int> { studentA.StudentId, studentB.StudentId }
        };

        var classId = await service.CoachCreateAsync(request, coachUser.UserId);

        var participantA = db.Participants.Single(p => p.IdCoachClass == classId && p.IdStudent == studentA.StudentId);

        // Only parentA responds; parentB has not yet responded
        await partSvc.ParentApproveEnrollmentAsync(participantA.ParticipantId, approve: true, callingUserId: parentA.UserId);

        var updatedClass = db.CoachClasses.Find(classId)!;
        updatedClass.Status.Should().Be((byte)CoachClassStatus.Requested,
            because: "class should stay Requested until all parents have responded");
    }

    //  Wrong parent cannot approve another parent's student

    [Fact]
    public async Task ParentApproveEnrollment_WrongParent_ThrowsUnauthorized()
    {
        var (db, coachUserId, modalityId, parentUserId, studentId, _) = SetupBase();

        var service = CreateClassService(db);
        var partSvc = CreateParticipantService(db);
        var start   = NextWeekdayAt(DayOfWeek.Monday, 10);

        var request = new CoachClassCoachCreateRequest
        {
            ModalityId      = modalityId,
            StartDatetime   = start,
            EndDatetime     = start.AddHours(1),
            MaxParticipants = 4,
            StudentIds      = new List<int> { studentId }
        };

        var classId    = await service.CoachCreateAsync(request, coachUserId);
        var participant = db.Participants.Single(p => p.IdCoachClass == classId);

        // Create a different parent who tries to approve
        var wrongParent = SeedData.SeedUserWithRole(db, "wrongParent", "parent");

        Func<Task> act = () => partSvc.ParentApproveEnrollmentAsync(
            participant.ParticipantId, approve: true, callingUserId: wrongParent.UserId);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }
}
