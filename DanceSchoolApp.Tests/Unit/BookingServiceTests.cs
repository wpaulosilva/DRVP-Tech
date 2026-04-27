using DanceSchoolApp.Server.Data;
using DanceSchoolApp.Server.DTOs.Classes;
using DanceSchoolApp.Server.Models;
using DanceSchoolApp.Server.Services.Scheduling;
using DanceSchoolApp.Tests.Helpers;
using FluentAssertions;

namespace DanceSchoolApp.Tests.Unit;

[Trait("Category", "Unit")]
public class BookingServiceTests
{
    // Fixed Monday — deterministic, no runtime date dependency.
    // 1 Jan 2024 is a Monday: DayOfWeek == 1.
    private static readonly DateOnly Monday = new DateOnly(2024, 1, 1);

    private static BookingService CreateService(AppDbContext db) => new BookingService(db);

    //  No availability 

    [Fact]
    public async Task GetAvailableSlotsAsync_NoAvailability_ReturnsEmptyList()
    {
        // Arrange — empty DB, no CoachAvailability rows
        var db = DbContextFactory.Create();
        var service = CreateService(db);

        // Act
        var result = await service.GetAvailableSlotsAsync(
            Monday, Monday.AddDays(4), coachId: null, modalityId: null);

        // Assert
        result.Should().BeEmpty();
    }

    //  Full slot with no bookings or blocks 

    [Fact]
    public async Task GetAvailableSlotsAsync_AvailabilityNoBookings_ReturnsFullSlot()
    {
        // Arrange
        var db = DbContextFactory.Create();
        SeedData.SeedRoles(db);
        var coachUser = SeedData.SeedUserWithRole(db, "coach1", "coach");
        var coach     = SeedData.SeedCoach(db, coachUser);

        db.CoachAvailabilities.Add(new CoachAvailability
        {
            IdCoach   = coach.CoachId,
            Weekday   = (byte)DayOfWeek.Monday, // 1
            StartTime = new TimeOnly(9, 0),
            EndTime   = new TimeOnly(17, 0)
        });
        db.SaveChanges();

        var service = CreateService(db);

        // Act
        var result = await service.GetAvailableSlotsAsync(
            Monday, Monday, coachId: null, modalityId: null);

        // Assert
        result.Should().HaveCount(1);
        result[0].Date.Should().Be(Monday);
        result[0].Slots.Should().HaveCount(1);
        result[0].Slots[0].StartTime.Should().Be(new TimeOnly(9, 0));
        result[0].Slots[0].EndTime.Should().Be(new TimeOnly(17, 0));
    }

    //  Approved booking punches a hole in the slot 

    [Fact]
    public async Task GetAvailableSlotsAsync_BookingSubtractsFromSlot()
    {
        // Arrange
        var db = DbContextFactory.Create();
        SeedData.SeedRoles(db);
        var modality  = SeedData.SeedModality(db);
        var studio    = SeedData.SeedStudio(db);
        var coachUser = SeedData.SeedUserWithRole(db, "coach1", "coach");
        var coach     = SeedData.SeedCoach(db, coachUser);

        db.CoachAvailabilities.Add(new CoachAvailability
        {
            IdCoach   = coach.CoachId,
            Weekday   = (byte)DayOfWeek.Monday,
            StartTime = new TimeOnly(9, 0),
            EndTime   = new TimeOnly(17, 0)
        });
        db.SaveChanges();

        // Approved booking 11:00–13:00 — must be subtracted
        SeedData.SeedCoachClass(db, coach, modality, studio, coachUser,
            start:           new DateTime(2024, 1, 1, 11, 0, 0, DateTimeKind.Utc),
            durationMinutes: 120,
            status:          (byte)CoachClassStatus.Approved);

        var service = CreateService(db);

        // Act
        var result = await service.GetAvailableSlotsAsync(
            Monday, Monday, coachId: null, modalityId: null);

        // Assert — busy 11:00–13:00 splits availability into two free windows
        result.Should().HaveCount(1);
        var slots = result[0].Slots;
        slots.Should().HaveCount(2);
        slots.Should().ContainSingle(s =>
            s.StartTime == new TimeOnly(9, 0) && s.EndTime == new TimeOnly(11, 0));
        slots.Should().ContainSingle(s =>
            s.StartTime == new TimeOnly(13, 0) && s.EndTime == new TimeOnly(17, 0));
    }

    //  Global blocked period subtracts 

    [Fact]
    public async Task GetAvailableSlotsAsync_BlockedPeriodSubtractsFromSlot()
    {
        // Arrange
        var db = DbContextFactory.Create();
        SeedData.SeedRoles(db);
        var coachUser = SeedData.SeedUserWithRole(db, "coach1", "coach");
        var coach     = SeedData.SeedCoach(db, coachUser);

        db.CoachAvailabilities.Add(new CoachAvailability
        {
            IdCoach   = coach.CoachId,
            Weekday   = (byte)DayOfWeek.Monday,
            StartTime = new TimeOnly(9, 0),
            EndTime   = new TimeOnly(17, 0)
        });

        // Global blocked period scope=1 covering 10:00–12:00
        db.BlockedPeriods.Add(new BlockedPeriod
        {
            Scope          = 1,
            StartDatetime  = new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc),
            EndDatetime    = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc)
        });
        db.SaveChanges();

        var service = CreateService(db);

        // Act
        var result = await service.GetAvailableSlotsAsync(
            Monday, Monday, coachId: null, modalityId: null);

        // Assert — 10:00–12:00 absent; windows are 09:00–10:00 and 12:00–17:00
        result.Should().HaveCount(1);
        var slots = result[0].Slots;
        slots.Should().HaveCount(2);
        slots.Should().ContainSingle(s =>
            s.StartTime == new TimeOnly(9, 0) && s.EndTime == new TimeOnly(10, 0));
        slots.Should().ContainSingle(s =>
            s.StartTime == new TimeOnly(12, 0) && s.EndTime == new TimeOnly(17, 0));
    }

    //  Cancelled booking is ignored 

    [Fact]
    public async Task GetAvailableSlotsAsync_CancelledBooking_DoesNotSubtract()
    {
        // Arrange
        var db = DbContextFactory.Create();
        SeedData.SeedRoles(db);
        var modality  = SeedData.SeedModality(db);
        var studio    = SeedData.SeedStudio(db);
        var coachUser = SeedData.SeedUserWithRole(db, "coach1", "coach");
        var coach     = SeedData.SeedCoach(db, coachUser);

        db.CoachAvailabilities.Add(new CoachAvailability
        {
            IdCoach   = coach.CoachId,
            Weekday   = (byte)DayOfWeek.Monday,
            StartTime = new TimeOnly(9, 0),
            EndTime   = new TimeOnly(17, 0)
        });
        db.SaveChanges();

        // Cancelled booking — the service filters it out before subtraction
        SeedData.SeedCoachClass(db, coach, modality, studio, coachUser,
            start:           new DateTime(2024, 1, 1, 11, 0, 0, DateTimeKind.Utc),
            durationMinutes: 120,
            status:          (byte)CoachClassStatus.Cancelled);

        var service = CreateService(db);

        // Act
        var result = await service.GetAvailableSlotsAsync(
            Monday, Monday, coachId: null, modalityId: null);

        // Assert — full 09:00–17:00 slot is untouched
        result.Should().HaveCount(1);
        result[0].Slots.Should().HaveCount(1);
        result[0].Slots[0].StartTime.Should().Be(new TimeOnly(9, 0));
        result[0].Slots[0].EndTime.Should().Be(new TimeOnly(17, 0));
    }

    //  Modality filter excludes wrong coach 

    [Fact]
    public async Task GetAvailableSlotsAsync_ModalityFilter_ExcludesCoachWithoutModality()
    {
        // Arrange
        var db = DbContextFactory.Create();
        SeedData.SeedRoles(db);
        var ballet = SeedData.SeedModality(db, "Ballet Clássico");

        var coachUserA = SeedData.SeedUserWithRole(db, "coachA", "coach");
        var coachUserB = SeedData.SeedUserWithRole(db, "coachB", "coach");
        var coachA     = SeedData.SeedCoach(db, coachUserA);
        var coachB     = SeedData.SeedCoach(db, coachUserB);

        // Link Ballet to Coach A only — Coach B has no modalities
        coachA.IdModalities.Add(ballet);
        db.SaveChanges();

        db.CoachAvailabilities.AddRange(
            new CoachAvailability
            {
                IdCoach   = coachA.CoachId,
                Weekday   = (byte)DayOfWeek.Monday,
                StartTime = new TimeOnly(9, 0),
                EndTime   = new TimeOnly(17, 0)
            },
            new CoachAvailability
            {
                IdCoach   = coachB.CoachId,
                Weekday   = (byte)DayOfWeek.Monday,
                StartTime = new TimeOnly(9, 0),
                EndTime   = new TimeOnly(17, 0)
            }
        );
        db.SaveChanges();

        var service = CreateService(db);

        // Act — filter by Ballet modality
        var result = await service.GetAvailableSlotsAsync(
            Monday, Monday, coachId: null, modalityId: ballet.ModalityId);

        // Assert — only Coach A's slot is returned; Coach B is excluded
        result.Should().HaveCount(1);
        result[0].Slots.Should().HaveCount(1);
        result[0].Slots[0].CoachId.Should().Be(coachA.CoachId);
    }
}
