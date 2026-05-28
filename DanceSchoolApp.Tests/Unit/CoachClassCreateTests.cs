using DanceSchoolApp.Server.Data;
using DanceSchoolApp.Server.DTOs.Classes;
using DanceSchoolApp.Server.Services.Classes;
using DanceSchoolApp.Server.Services.Social;
using DanceSchoolApp.Tests.Helpers;
using FluentAssertions;

namespace DanceSchoolApp.Tests.Unit;

[Trait("Category", "Unit")]
public class CoachClassCreateTests
{
    private static CoachClassService CreateService(AppDbContext db) =>
        new CoachClassService(db, new NotificationService(db));

    private static DateTime NextWeekdayAt(DayOfWeek target, int hour)
    {
        var today = DateTime.UtcNow.Date;
        var days  = ((int)target - (int)today.DayOfWeek + 7) % 7;
        if (days == 0) days = 7;
        return DateTime.SpecifyKind(today.AddDays(days).AddHours(hour), DateTimeKind.Utc);
    }

    private static (AppDbContext db, int coachId, int modalityId, int parentUserId, int studentId, DanceSchoolApp.Server.Models.Coach coach)
        SetupBase()
    {
        var db = DbContextFactory.Create();
        SeedData.SeedRoles(db);
        var modality   = SeedData.SeedModality(db);
        var studio     = SeedData.SeedStudio(db);
        studio.IdModalities.Add(modality);
        db.SaveChanges();

        var coachUser  = SeedData.SeedUserWithRole(db, "coach1", "coach");
        var coach      = SeedData.SeedCoach(db, coachUser);
        coach.IdModalities.Add(modality);
        db.SaveChanges();

        var parentUser = SeedData.SeedUserWithRole(db, "parent1", "parent");
        var student    = SeedData.SeedStudent(db, parentUser);
        student.IdModalities.Add(modality);
        db.SaveChanges();

        return (db, coach.CoachId, modality.ModalityId, parentUser.UserId, student.StudentId, coach);
    }

    //  availability missing

    [Fact]
    public async Task ParentCreateAsync_CoachHasNoAvailability_ThrowsInvalidOperation()
    {
        var (db, coachId, modalityId, parentUserId, studentId, _) = SetupBase();
        var service = CreateService(db);
        var start = NextWeekdayAt(DayOfWeek.Monday, 10);

        var request = new CoachClassParentCreateRequest
        {
            CoachId       = coachId,
            ModalityId    = modalityId,
            StartDatetime = start,
            EndDatetime   = start.AddHours(1),
            StudentId     = studentId
        };

        Func<Task> act = () => service.ParentCreateAsync(request, parentUserId);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*availability*");
    }

    //  availability present but expired

    [Fact]
    public async Task ParentCreateAsync_CoachAvailabilityExpired_ThrowsInvalidOperation()
    {
        var (db, coachId, modalityId, parentUserId, studentId, coach) = SetupBase();
        SeedData.SeedCoachAvailability(db, coach,
            weekday:   (byte)DayOfWeek.Monday,
            startTime: new TimeOnly(9, 0),
            endTime:   new TimeOnly(12, 0),
            validUntil: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)));

        var service = CreateService(db);
        var start = NextWeekdayAt(DayOfWeek.Monday, 10);

        var request = new CoachClassParentCreateRequest
        {
            CoachId       = coachId,
            ModalityId    = modalityId,
            StartDatetime = start,
            EndDatetime   = start.AddHours(1),
            StudentId     = studentId
        };

        Func<Task> act = () => service.ParentCreateAsync(request, parentUserId);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*availability*");
    }

    //  availability on wrong weekday

    [Fact]
    public async Task ParentCreateAsync_AvailabilityOnWrongWeekday_ThrowsInvalidOperation()
    {
        var (db, coachId, modalityId, parentUserId, studentId, coach) = SetupBase();
        SeedData.SeedCoachAvailability(db, coach,
            weekday:   (byte)DayOfWeek.Tuesday,
            startTime: new TimeOnly(9, 0),
            endTime:   new TimeOnly(12, 0));

        var service = CreateService(db);
        var start = NextWeekdayAt(DayOfWeek.Monday, 10);

        var request = new CoachClassParentCreateRequest
        {
            CoachId       = coachId,
            ModalityId    = modalityId,
            StartDatetime = start,
            EndDatetime   = start.AddHours(1),
            StudentId     = studentId
        };

        Func<Task> act = () => service.ParentCreateAsync(request, parentUserId);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*availability*");
    }

    //  matching availability

    [Fact]
    public async Task ParentCreateAsync_CoachHasMatchingAvailability_ReturnsPositiveClassId()
    {
        var (db, coachId, modalityId, parentUserId, studentId, coach) = SetupBase();
        SeedData.SeedCoachAvailability(db, coach,
            weekday:   (byte)DayOfWeek.Monday,
            startTime: new TimeOnly(9, 0),
            endTime:   new TimeOnly(12, 0));

        var service = CreateService(db);
        var start = NextWeekdayAt(DayOfWeek.Monday, 10);

        var request = new CoachClassParentCreateRequest
        {
            CoachId       = coachId,
            ModalityId    = modalityId,
            StartDatetime = start,
            EndDatetime   = start.AddHours(1),
            StudentId     = studentId
        };

        var classId = await service.ParentCreateAsync(request, parentUserId);

        classId.Should().BeGreaterThan(0);
    }

    //  class is always individual (MaxParticipants = 1)

    [Fact]
    public async Task ParentCreateAsync_CreatedClass_HasMaxParticipantsOfOne()
    {
        var (db, coachId, modalityId, parentUserId, studentId, coach) = SetupBase();
        SeedData.SeedCoachAvailability(db, coach,
            weekday:   (byte)DayOfWeek.Monday,
            startTime: new TimeOnly(9, 0),
            endTime:   new TimeOnly(12, 0));

        var service = CreateService(db);
        var start = NextWeekdayAt(DayOfWeek.Monday, 10);

        var request = new CoachClassParentCreateRequest
        {
            CoachId       = coachId,
            ModalityId    = modalityId,
            StartDatetime = start,
            EndDatetime   = start.AddHours(1),
            StudentId     = studentId
        };

        var classId = await service.ParentCreateAsync(request, parentUserId);

        var coachClass = db.CoachClasses.Find(classId)!;
        coachClass.MaxParticipants.Should().Be(1);
        coachClass.ClassOrigin.Should().Be((byte)ClassOrigin.ParentCreated);
    }

    //  student that does not belong to the parent is rejected

    [Fact]
    public async Task ParentCreateAsync_StudentNotOwnedByParent_ThrowsKeyNotFound()
    {
        var (db, coachId, modalityId, parentUserId, _, coach) = SetupBase();
        SeedData.SeedCoachAvailability(db, coach,
            weekday:   (byte)DayOfWeek.Monday,
            startTime: new TimeOnly(9, 0),
            endTime:   new TimeOnly(12, 0));

        // Create a different parent and student
        var otherParent = SeedData.SeedUserWithRole(db, "otherParent", "parent");
        var otherStudent = SeedData.SeedStudent(db, otherParent, "Zé");
        otherStudent.IdModalities.Add(db.Modalities.Find(modalityId)!);
        db.SaveChanges();

        var service = CreateService(db);
        var start = NextWeekdayAt(DayOfWeek.Monday, 10);

        var request = new CoachClassParentCreateRequest
        {
            CoachId       = coachId,
            ModalityId    = modalityId,
            StartDatetime = start,
            EndDatetime   = start.AddHours(1),
            StudentId     = otherStudent.StudentId
        };

        Func<Task> act = () => service.ParentCreateAsync(request, parentUserId);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }
}
