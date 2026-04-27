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
        // Ensure coach teaches the modality used in tests
        coach.IdModalities.Add(modality);
        db.SaveChanges();
        var parentUser = SeedData.SeedUserWithRole(db, "parent1", "parent");
        var student    = SeedData.SeedStudent(db, parentUser);

        return (db, coach.CoachId, modality.ModalityId, parentUser.UserId, student.StudentId, coach);
    }

    //  availability missing 

    [Fact]
    public async Task CreateAsync_CoachHasNoAvailability_ThrowsInvalidOperation()
    {
        var (db, coachId, modalityId, parentUserId, studentId, _) = SetupBase();
        var service = CreateService(db);
        var start = NextWeekdayAt(DayOfWeek.Monday, 10);

        var request = new CoachClassCreateRequest
        {
            CoachId         = coachId,
            ModalityId      = modalityId,
            StartDatetime   = start,
            EndDatetime     = start.AddHours(1),
            MaxParticipants = 4,
            StudentIds      = new List<int> { studentId }
        };

        Func<Task> act = () => service.CreateAsync(request, parentUserId);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*availability*");
    }

    //  availability present but expired 

    [Fact]
    public async Task CreateAsync_CoachAvailabilityExpired_ThrowsInvalidOperation()
    {
        var (db, coachId, modalityId, parentUserId, studentId, coach) = SetupBase();
        SeedData.SeedCoachAvailability(db, coach,
            weekday:   (byte)DayOfWeek.Monday,
            startTime: new TimeOnly(9, 0),
            endTime:   new TimeOnly(12, 0),
            validUntil: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)));

        var service = CreateService(db);
        var start = NextWeekdayAt(DayOfWeek.Monday, 10);

        var request = new CoachClassCreateRequest
        {
            CoachId         = coachId,
            ModalityId      = modalityId,
            StartDatetime   = start,
            EndDatetime     = start.AddHours(1),
            MaxParticipants = 4,
            StudentIds      = new List<int> { studentId }
        };

        Func<Task> act = () => service.CreateAsync(request, parentUserId);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*availability*");
    }

    //  availability on wrong weekday 

    [Fact]
    public async Task CreateAsync_AvailabilityOnWrongWeekday_ThrowsInvalidOperation()
    {
        var (db, coachId, modalityId, parentUserId, studentId, coach) = SetupBase();
        // Coach available Tuesday only; class is on Monday
        SeedData.SeedCoachAvailability(db, coach,
            weekday:   (byte)DayOfWeek.Tuesday,
            startTime: new TimeOnly(9, 0),
            endTime:   new TimeOnly(12, 0));

        var service = CreateService(db);
        var start = NextWeekdayAt(DayOfWeek.Monday, 10);

        var request = new CoachClassCreateRequest
        {
            CoachId         = coachId,
            ModalityId      = modalityId,
            StartDatetime   = start,
            EndDatetime     = start.AddHours(1),
            MaxParticipants = 4,
            StudentIds      = new List<int> { studentId }
        };

        Func<Task> act = () => service.CreateAsync(request, parentUserId);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*availability*");
    }

    //  matching availability 

    [Fact]
    public async Task CreateAsync_CoachHasMatchingAvailability_ReturnsPositiveClassId()
    {
        var (db, coachId, modalityId, parentUserId, studentId, coach) = SetupBase();
        SeedData.SeedCoachAvailability(db, coach,
            weekday:   (byte)DayOfWeek.Monday,
            startTime: new TimeOnly(9, 0),
            endTime:   new TimeOnly(12, 0));

        var service = CreateService(db);
        var start = NextWeekdayAt(DayOfWeek.Monday, 10);

        var request = new CoachClassCreateRequest
        {
            CoachId         = coachId,
            ModalityId      = modalityId,
            StartDatetime   = start,
            EndDatetime     = start.AddHours(1),
            MaxParticipants = 4,
            StudentIds      = new List<int> { studentId }
        };

        var classId = await service.CreateAsync(request, parentUserId);

        classId.Should().BeGreaterThan(0);
    }
}
