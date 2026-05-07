using DanceSchoolApp.Server.Data;
using DanceSchoolApp.Server.DTOs.Classes;
using DanceSchoolApp.Server.Models;
using DanceSchoolApp.Server.Services.Classes;
using DanceSchoolApp.Server.Services.Social;
using DanceSchoolApp.Tests.Helpers;
using FluentAssertions;

namespace DanceSchoolApp.Tests.Unit;

/// <summary>
/// BPMN 1 — Create / Enroll in Classes
/// Covers the staff-respond and coach-respond transitions, plus cancel.
/// Happy paths: Requested→StaffApproved, StaffApproved→Approved/Rejected.
/// Sad  paths: wrong state, wrong coach identity.
/// </summary>
[Trait("Category", "Unit")]
public class CoachClassTransitionTests
{
    private static CoachClassService CreateService(AppDbContext db) =>
        new CoachClassService(db, new NotificationService(db));

    private static (AppDbContext db, Coach coach, int coachUserId,
                    int otherCoachUserId, Modality modality, Studio studio, User parentUser)
        SetupBase()
    {
        var db = DbContextFactory.Create();
        SeedData.SeedRoles(db);

        var modality = SeedData.SeedModality(db);
        var studio   = SeedData.SeedStudio(db);
        studio.IdModalities.Add(modality);
        db.SaveChanges();

        var coachUser  = SeedData.SeedUserWithRole(db, "coach1", "coach");
        var coach      = SeedData.SeedCoach(db, coachUser);
        coach.IdModalities.Add(modality);
        db.SaveChanges();

        // A second coach whose ID should be rejected for operations on the first coach's class
        var otherCoachUser = SeedData.SeedUserWithRole(db, "coach2", "coach");
        var otherCoach     = SeedData.SeedCoach(db, otherCoachUser);
        db.SaveChanges();

        var parentUser = SeedData.SeedUserWithRole(db, "parent1", "parent");

        return (db, coach, coachUser.UserId, otherCoachUser.UserId, modality, studio, parentUser);
    }

    private static CoachClass SeedClassWithStatus(
        AppDbContext db, Coach coach, Modality modality, Studio studio,
        User createdBy, CoachClassStatus status)
    {
        var cls = SeedData.SeedCoachClass(db, coach, modality, studio, createdBy,
            status: (byte)status);
        return cls;
    }

    // ─── StaffRespondAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task StaffRespondAsync_Approve_SetsStatusToStaffApproved()
    {
        // Arrange
        var (db, coach, _, _, modality, studio, parentUser) = SetupBase();
        var cls     = SeedClassWithStatus(db, coach, modality, studio, parentUser,
                          CoachClassStatus.Requested);
        var service = CreateService(db);

        // Act
        await service.StaffRespondAsync(cls.ClassId, approve: true, reason: null);

        // Assert — reload to verify the DB write
        var updated = db.CoachClasses.Find(cls.ClassId)!;
        updated.Status.Should().Be((byte)CoachClassStatus.StaffApproved,
            because: "staff approving a Requested class must move it to StaffApproved");
    }

    [Fact]
    public async Task StaffRespondAsync_Reject_SetsStatusToRejected()
    {
        // Arrange
        var (db, coach, _, _, modality, studio, parentUser) = SetupBase();
        var cls     = SeedClassWithStatus(db, coach, modality, studio, parentUser,
                          CoachClassStatus.Requested);
        var service = CreateService(db);

        // Act
        await service.StaffRespondAsync(cls.ClassId, approve: false, reason: "Room unavailable");

        // Assert
        var updated = db.CoachClasses.Find(cls.ClassId)!;
        updated.Status.Should().Be((byte)CoachClassStatus.Rejected,
            because: "staff rejecting a Requested class must set status to Rejected");
    }

    [Fact]
    public async Task StaffRespondAsync_WhenClassNotRequested_ThrowsInvalidOperation()
    {
        // Arrange — class is already Approved, not Requested
        var (db, coach, _, _, modality, studio, parentUser) = SetupBase();
        var cls     = SeedClassWithStatus(db, coach, modality, studio, parentUser,
                          CoachClassStatus.Approved);
        var service = CreateService(db);

        // Act
        Func<Task> act = () => service.StaffRespondAsync(cls.ClassId, approve: true, reason: null);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>(
            because: "staff-respond is only valid on Requested classes");
    }

    // ─── CoachRespondAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task CoachRespondAsync_Accept_SetsStatusToApproved()
    {
        // Arrange
        var (db, coach, coachUserId, _, modality, studio, parentUser) = SetupBase();
        var cls     = SeedClassWithStatus(db, coach, modality, studio, parentUser,
                          CoachClassStatus.StaffApproved);
        var service = CreateService(db);

        // Act
        await service.CoachRespondAsync(cls.ClassId, coachUserId, accept: true, reason: null);

        // Assert
        var updated = db.CoachClasses.Find(cls.ClassId)!;
        updated.Status.Should().Be((byte)CoachClassStatus.Approved,
            because: "coach accepting a StaffApproved class must move it to Approved");
    }

    [Fact]
    public async Task CoachRespondAsync_Reject_SetsStatusToRejected()
    {
        // Arrange
        var (db, coach, coachUserId, _, modality, studio, parentUser) = SetupBase();
        var cls     = SeedClassWithStatus(db, coach, modality, studio, parentUser,
                          CoachClassStatus.StaffApproved);
        var service = CreateService(db);

        // Act
        await service.CoachRespondAsync(cls.ClassId, coachUserId, accept: false,
            reason: "Schedule conflict");

        // Assert
        var updated = db.CoachClasses.Find(cls.ClassId)!;
        updated.Status.Should().Be((byte)CoachClassStatus.Rejected,
            because: "coach rejecting a StaffApproved class must set status to Rejected");
    }

    [Fact]
    public async Task CoachRespondAsync_WrongCoach_ThrowsUnauthorizedAccess()
    {
        // Arrange — class belongs to coach1; caller is coach2
        var (db, coach, _, otherCoachUserId, modality, studio, parentUser) = SetupBase();
        var cls     = SeedClassWithStatus(db, coach, modality, studio, parentUser,
                          CoachClassStatus.StaffApproved);
        var service = CreateService(db);

        // Act
        Func<Task> act = () =>
            service.CoachRespondAsync(cls.ClassId, otherCoachUserId, accept: true, reason: null);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>(
            because: "a coach may only respond to their own classes");
    }

    [Fact]
    public async Task CoachRespondAsync_WhenClassNotStaffApproved_ThrowsInvalidOperation()
    {
        // Arrange — class is still Requested, not StaffApproved
        var (db, coach, coachUserId, _, modality, studio, parentUser) = SetupBase();
        var cls     = SeedClassWithStatus(db, coach, modality, studio, parentUser,
                          CoachClassStatus.Requested);
        var service = CreateService(db);

        // Act
        Func<Task> act = () =>
            service.CoachRespondAsync(cls.ClassId, coachUserId, accept: true, reason: null);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>(
            because: "coach-respond requires the class to be in StaffApproved state");
    }

    // ─── CancelAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task CancelAsync_ApprovedClass_SetsStatusToCancelled()
    {
        // Arrange
        var (db, coach, _, _, modality, studio, parentUser) = SetupBase();
        var cls     = SeedClassWithStatus(db, coach, modality, studio, parentUser,
                          CoachClassStatus.Approved);
        var service = CreateService(db);

        // Act
        await service.CancelAsync(cls.ClassId);

        // Assert
        var updated = db.CoachClasses.Find(cls.ClassId)!;
        updated.Status.Should().Be((byte)CoachClassStatus.Cancelled,
            because: "cancelling an Approved class must set status to Cancelled");
    }

    [Fact]
    public async Task CancelAsync_ValidatedClass_ThrowsInvalidOperation()
    {
        // Arrange — Validated is a terminal state; cancellation is not allowed
        var (db, coach, _, _, modality, studio, parentUser) = SetupBase();
        var cls     = SeedClassWithStatus(db, coach, modality, studio, parentUser,
                          CoachClassStatus.Validated);
        var service = CreateService(db);

        // Act
        Func<Task> act = () => service.CancelAsync(cls.ClassId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>(
            because: "a Validated class is terminal and cannot be cancelled");
    }

    [Fact]
    public async Task CancelAsync_RejectedClass_ThrowsInvalidOperation()
    {
        // Arrange — Rejected is also terminal
        var (db, coach, _, _, modality, studio, parentUser) = SetupBase();
        var cls     = SeedClassWithStatus(db, coach, modality, studio, parentUser,
                          CoachClassStatus.Rejected);
        var service = CreateService(db);

        // Act
        Func<Task> act = () => service.CancelAsync(cls.ClassId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>(
            because: "a Rejected class cannot be cancelled");
    }
}
