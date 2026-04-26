using DanceSchoolApp.Server.DTOs.Classes;
using DanceSchoolApp.Server.Models;
using DanceSchoolApp.Server.Services;
using DanceSchoolApp.Tests.Helpers;
using FluentAssertions;

namespace DanceSchoolApp.Tests.Unit;
/*
[Trait("Category", "Unit")]
public class BillingServiceTests
{
    // ─── Factory helpers ──────────────────────────────────────────────────────

    private static BillingService CreateService(DanceSchoolApp.Server.Data.AppDbContext db)
    {
        var appSettings = new AppSettingService(db);
        return new BillingService(db, appSettings);
    }

    // ─── Student billing ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetStudentBilling_WeekdayClass_AppliesWeekdayRate()
    {
        // Arrange
        var db = DbContextFactory.Create();
        SeedData.SeedRoles(db);
        SeedData.SeedAppSettings(db);
        var modality   = SeedData.SeedModality(db);
        var studio     = SeedData.SeedStudio(db);
        var parentUser = SeedData.SeedUserWithRole(db, "parent1", "parent");
        var coachUser  = SeedData.SeedUserWithRole(db, "coach1", "coach");
        var coach      = SeedData.SeedCoach(db, coachUser);
        var student    = SeedData.SeedStudent(db, parentUser);

        // Monday 1 Jan 2024 10:00 UTC — weekday, 2-hour class
        var start = new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var coachClass = SeedData.SeedCoachClass(db, coach, modality, studio, coachUser,
            start: start, durationMinutes: 120, status: (byte)CoachClassStatus.Validated);

        db.Participants.Add(new Participant
        {
            IdCoachClass     = coachClass.ClassId,
            IdStudent        = student.StudentId,
            JoinedAt         = DateOnly.FromDateTime(start),
            ValidationStatus = 0,

        });
        db.SaveChanges();

        var service = CreateService(db);

        // Act
        var result = await service.GetStudentBillingAsync(2024, 1, null, 1, 25);

        // Assert
        result.Items.Should().HaveCount(1);
        result.Items[0].TotalAmount.Should().Be(72.00m); // 2h * 36.00
    }

    [Fact]
    public async Task GetStudentBilling_WeekendClass_AppliesWeekendRate()
    {
        // Arrange
        var db = DbContextFactory.Create();
        SeedData.SeedRoles(db);
        SeedData.SeedAppSettings(db);
        var modality   = SeedData.SeedModality(db);
        var studio     = SeedData.SeedStudio(db);
        var parentUser = SeedData.SeedUserWithRole(db, "parent1", "parent");
        var coachUser  = SeedData.SeedUserWithRole(db, "coach1", "coach");
        var coach      = SeedData.SeedCoach(db, coachUser);
        var student    = SeedData.SeedStudent(db, parentUser);

        // Saturday 6 Jan 2024 10:00 UTC — weekend, 2-hour class
        var start = new DateTime(2024, 1, 6, 10, 0, 0, DateTimeKind.Utc);
        var coachClass = SeedData.SeedCoachClass(db, coach, modality, studio, coachUser,
            start: start, durationMinutes: 120, status: (byte)CoachClassStatus.Validated);

        db.Participants.Add(new Participant
        {
            IdCoachClass     = coachClass.ClassId,
            IdStudent        = student.StudentId,
            JoinedAt         = DateOnly.FromDateTime(start),
            ValidationStatus = 0,

        });
        db.SaveChanges();

        var service = CreateService(db);

        // Act
        var result = await service.GetStudentBillingAsync(2024, 1, null, 1, 25);

        // Assert
        result.Items.Should().HaveCount(1);
        result.Items[0].TotalAmount.Should().Be(86.40m); // 2h * 43.20
    }

    [Fact]
    public async Task GetStudentBilling_NonValidatedClass_NotIncluded()
    {
        // Arrange
        var db = DbContextFactory.Create();
        SeedData.SeedRoles(db);
        SeedData.SeedAppSettings(db);
        var modality   = SeedData.SeedModality(db);
        var studio     = SeedData.SeedStudio(db);
        var parentUser = SeedData.SeedUserWithRole(db, "parent1", "parent");
        var coachUser  = SeedData.SeedUserWithRole(db, "coach1", "coach");
        var coach      = SeedData.SeedCoach(db, coachUser);
        var student    = SeedData.SeedStudent(db, parentUser);

        // Class is Finished (4) — not yet Validated (5)
        var start = new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var coachClass = SeedData.SeedCoachClass(db, coach, modality, studio, coachUser,
            start: start, durationMinutes: 60, status: (byte)CoachClassStatus.Finished);

        db.Participants.Add(new Participant
        {
            IdCoachClass     = coachClass.ClassId,
            IdStudent        = student.StudentId,
            JoinedAt         = DateOnly.FromDateTime(start),
            ValidationStatus = 0,

        });
        db.SaveChanges();

        var service = CreateService(db);

        // Act
        var result = await service.GetStudentBillingAsync(2024, 1, null, 1, 25);

        // Assert
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetStudentBilling_TwoClassesSameStudent_SumsCorrectly()
    {
        // Arrange
        var db = DbContextFactory.Create();
        SeedData.SeedRoles(db);
        SeedData.SeedAppSettings(db);
        var modality   = SeedData.SeedModality(db);
        var studio     = SeedData.SeedStudio(db);
        var parentUser = SeedData.SeedUserWithRole(db, "parent1", "parent");
        var coachUser  = SeedData.SeedUserWithRole(db, "coach1", "coach");
        var coach      = SeedData.SeedCoach(db, coachUser);
        var student    = SeedData.SeedStudent(db, parentUser);

        // 1h weekday (Monday 1 Jan 2024)
        var weekdayStart = new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var weekdayClass = SeedData.SeedCoachClass(db, coach, modality, studio, coachUser,
            start: weekdayStart, durationMinutes: 60, status: (byte)CoachClassStatus.Validated);

        // 1h weekend (Saturday 6 Jan 2024)
        var weekendStart = new DateTime(2024, 1, 6, 10, 0, 0, DateTimeKind.Utc);
        var weekendClass = SeedData.SeedCoachClass(db, coach, modality, studio, coachUser,
            start: weekendStart, durationMinutes: 60, status: (byte)CoachClassStatus.Validated);

        db.Participants.AddRange(
            new Participant
            {
                IdCoachClass     = weekdayClass.ClassId,
                IdStudent        = student.StudentId,
                JoinedAt         = DateOnly.FromDateTime(weekdayStart),
                ValidationStatus = 0,

            },
            new Participant
            {
                IdCoachClass     = weekendClass.ClassId,
                IdStudent        = student.StudentId,
                JoinedAt         = DateOnly.FromDateTime(weekendStart),
                ValidationStatus = 0,

            }
        );
        db.SaveChanges();

        var service = CreateService(db);

        // Act
        var result = await service.GetStudentBillingAsync(2024, 1, null, 1, 25);

        // Assert
        result.Items.Should().HaveCount(1);
        result.Items[0].TotalAmount.Should().Be(79.20m); // 36.00 + 43.20
    }

    [Fact]
    public async Task GetStudentBilling_Summary_TotalRevenueMatchesItemsSum()
    {
        // Arrange
        var db = DbContextFactory.Create();
        SeedData.SeedRoles(db);
        SeedData.SeedAppSettings(db);
        var modality    = SeedData.SeedModality(db);
        var studio      = SeedData.SeedStudio(db);
        var parentUser1 = SeedData.SeedUserWithRole(db, "parent1", "parent");
        var parentUser2 = SeedData.SeedUserWithRole(db, "parent2", "parent");
        var coachUser   = SeedData.SeedUserWithRole(db, "coach1", "coach");
        var coach       = SeedData.SeedCoach(db, coachUser);
        var student1    = SeedData.SeedStudent(db, parentUser1, "Ana");
        var student2    = SeedData.SeedStudent(db, parentUser2, "Beatriz");

        // Two separate 1h weekday classes (Monday + Tuesday), one student per class
        var start1 = new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc); // Monday
        var start2 = new DateTime(2024, 1, 2, 10, 0, 0, DateTimeKind.Utc); // Tuesday
        var class1 = SeedData.SeedCoachClass(db, coach, modality, studio, coachUser,
            start: start1, durationMinutes: 60, status: (byte)CoachClassStatus.Validated);
        var class2 = SeedData.SeedCoachClass(db, coach, modality, studio, coachUser,
            start: start2, durationMinutes: 60, status: (byte)CoachClassStatus.Validated);

        db.Participants.AddRange(
            new Participant
            {
                IdCoachClass     = class1.ClassId,
                IdStudent        = student1.StudentId,
                JoinedAt         = DateOnly.FromDateTime(start1),
                ValidationStatus = 0,

            },
            new Participant
            {
                IdCoachClass     = class2.ClassId,
                IdStudent        = student2.StudentId,
                JoinedAt         = DateOnly.FromDateTime(start2),
                ValidationStatus = 0,

            }
        );
        db.SaveChanges();

        var service = CreateService(db);

        // Act
        var result = await service.GetStudentBillingAsync(2024, 1, null, 1, 25);

        // Assert
        result.Summary.TotalRevenue.Should().Be(72.00m);  // 2 * (1h * 36.00)
        result.Summary.TotalStudents.Should().Be(2);
    }

    // ─── Coach billing ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetCoachBilling_WeekdayClass_AppliesWeekdayRate()
    {
        // Arrange
        var db = DbContextFactory.Create();
        SeedData.SeedRoles(db);
        SeedData.SeedAppSettings(db);
        var modality  = SeedData.SeedModality(db);
        var studio    = SeedData.SeedStudio(db);
        var coachUser = SeedData.SeedUserWithRole(db, "coach1", "coach");
        var coach     = SeedData.SeedCoach(db, coachUser);

        // Monday 1 Jan 2024 — weekday, 2-hour class
        var start = new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        SeedData.SeedCoachClass(db, coach, modality, studio, coachUser,
            start: start, durationMinutes: 120, status: (byte)CoachClassStatus.Validated);

        var service = CreateService(db);

        // Act
        var result = await service.GetCoachBillingAsync(2024, 1, null, 1, 25);

        // Assert
        result.Items.Should().HaveCount(1);
        result.Items[0].TotalAmount.Should().Be(72.00m); // 2h * 36.00 (weekday rate)
    }

    [Fact]
    public async Task GetCoachBilling_WeekendClass_AppliesWeekendRate()
    {
        // Arrange
        var db = DbContextFactory.Create();
        SeedData.SeedRoles(db);
        SeedData.SeedAppSettings(db);
        var modality  = SeedData.SeedModality(db);
        var studio    = SeedData.SeedStudio(db);
        var coachUser = SeedData.SeedUserWithRole(db, "coach1", "coach");
        var coach     = SeedData.SeedCoach(db, coachUser);

        // Saturday 6 Jan 2024 — weekend, 2-hour class
        var start = new DateTime(2024, 1, 6, 10, 0, 0, DateTimeKind.Utc);
        SeedData.SeedCoachClass(db, coach, modality, studio, coachUser,
            start: start, durationMinutes: 120, status: (byte)CoachClassStatus.Validated);

        var service = CreateService(db);

        // Act
        var result = await service.GetCoachBillingAsync(2024, 1, null, 1, 25);

        // Assert
        result.Items.Should().HaveCount(1);
        result.Items[0].TotalAmount.Should().Be(86.40m); // 2h * 43.20 (weekend rate)
    }
}
*/