using DanceSchoolApp.Server.Data;
using DanceSchoolApp.Server.DTOs.Classes;
using DanceSchoolApp.Server.Models;
using DanceSchoolApp.Server.Services.Classes;
using DanceSchoolApp.Server.Services.Social;
using DanceSchoolApp.Tests.Helpers;
using FluentAssertions;

namespace DanceSchoolApp.Tests.Unit;

/// <summary>
/// BPMN 2 — Validate Classes (48h window)
/// Covers CoachValidateAsync and StaffValidateAsync transitions,
/// including the auto-advance to Pending when all parties have responded.
/// Happy paths: Finished→(coach validated), Pending→Validated.
/// Sad  paths: wrong coach, already validated, wrong state.
/// </summary>
[Trait("Category", "Unit")]
public class CoachClassValidationTests
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

        var coachUser = SeedData.SeedUserWithRole(db, "coach1_val", "coach");
        var coach     = SeedData.SeedCoach(db, coachUser);
        coach.IdModalities.Add(modality);
        db.SaveChanges();

        var otherCoachUser = SeedData.SeedUserWithRole(db, "coach2_val", "coach");
        SeedData.SeedCoach(db, otherCoachUser);
        db.SaveChanges();

        var parentUser = SeedData.SeedUserWithRole(db, "parent1_val", "parent");

        return (db, coach, coachUser.UserId, otherCoachUser.UserId, modality, studio, parentUser);
    }

    private static CoachClass SeedClassWithStatus(
        AppDbContext db, Coach coach, Modality modality, Studio studio,
        User createdBy, CoachClassStatus status,
        CoachValidationStatus coachValStatus = CoachValidationStatus.Pending,
        DateTime? coachValidatedAt = null)
    {
        var cls = SeedData.SeedCoachClass(db, coach, modality, studio, createdBy,
            status: (byte)status);
        cls.CoachValidationStatus = (byte)coachValStatus;
        cls.CoachValidatedAt = coachValidatedAt;
        db.SaveChanges();
        return cls;
    }

    // ─── CoachValidateAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task CoachValidateAsync_DidTeach_SetsStatusConfirmed()
    {
        // Arrange
        var (db, coach, coachUserId, _, modality, studio, parentUser) = SetupBase();
        var cls     = SeedClassWithStatus(db, coach, modality, studio, parentUser,
                          CoachClassStatus.Finished);
        var service = CreateService(db);

        // Act
        await service.CoachValidateAsync(cls.ClassId, coachUserId, didTeach: true);

        // Assert
        var updated = db.CoachClasses.Find(cls.ClassId)!;
        updated.CoachValidationStatus.Should().Be((byte)CoachValidationStatus.Confirmed,
            because: "coach confirming they taught must set CoachValidationStatus to Confirmed");
    }

    [Fact]
    public async Task CoachValidateAsync_DidNotTeach_SetsStatusDenied()
    {
        // Arrange
        var (db, coach, coachUserId, _, modality, studio, parentUser) = SetupBase();
        var cls     = SeedClassWithStatus(db, coach, modality, studio, parentUser,
                          CoachClassStatus.Finished);
        var service = CreateService(db);

        // Act
        await service.CoachValidateAsync(cls.ClassId, coachUserId, didTeach: false);

        // Assert
        var updated = db.CoachClasses.Find(cls.ClassId)!;
        updated.CoachValidationStatus.Should().Be((byte)CoachValidationStatus.Denied,
            because: "coach denying they taught must set CoachValidationStatus to Denied");
    }

    [Fact]
    public async Task CoachValidateAsync_WrongCoach_ThrowsUnauthorizedAccess()
    {
        // Arrange — class belongs to coach1; caller is coach2
        var (db, coach, _, otherCoachUserId, modality, studio, parentUser) = SetupBase();
        var cls     = SeedClassWithStatus(db, coach, modality, studio, parentUser,
                          CoachClassStatus.Finished);
        var service = CreateService(db);

        // Act
        Func<Task> act = () =>
            service.CoachValidateAsync(cls.ClassId, otherCoachUserId, didTeach: true);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>(
            because: "a coach may only validate their own classes");
    }

    [Fact]
    public async Task CoachValidateAsync_AlreadyValidated_ThrowsInvalidOperation()
    {
        // Arrange — CoachValidationStatus is already Confirmed, not Pending
        var (db, coach, coachUserId, _, modality, studio, parentUser) = SetupBase();
        var cls     = SeedClassWithStatus(db, coach, modality, studio, parentUser,
                          CoachClassStatus.Finished,
                          coachValStatus: CoachValidationStatus.Confirmed);
        var service = CreateService(db);

        // Act
        Func<Task> act = () =>
            service.CoachValidateAsync(cls.ClassId, coachUserId, didTeach: true);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>(
            because: "a class already validated by the coach cannot be validated a second time");
    }

    [Fact]
    public async Task CoachValidateAsync_WrongState_ThrowsInvalidOperation()
    {
        // Arrange — Approved is not a valid state for coach validation
        var (db, coach, coachUserId, _, modality, studio, parentUser) = SetupBase();
        var cls     = SeedClassWithStatus(db, coach, modality, studio, parentUser,
                          CoachClassStatus.Approved);
        var service = CreateService(db);

        // Act
        Func<Task> act = () =>
            service.CoachValidateAsync(cls.ClassId, coachUserId, didTeach: true);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>(
            because: "coach validation requires the class to be in Finished or Pending state");
    }

    [Fact]
    public async Task CoachValidateAsync_AllParticipantsAlreadyResponded_AdvancesToPending()
    {
        // Arrange — class is Finished; participant has already validated (non-Pending status)
        var (db, coach, coachUserId, _, modality, studio, parentUser) = SetupBase();
        var cls     = SeedClassWithStatus(db, coach, modality, studio, parentUser,
                          CoachClassStatus.Finished);

        var student = SeedData.SeedStudent(db, parentUser);
        db.Participants.Add(new Participant
        {
            IdCoachClass      = cls.ClassId,
            IdStudent         = student.StudentId,
            JoinedAt          = DateOnly.FromDateTime(DateTime.UtcNow),
            ValidationStatus  = (byte)ParticipantValidationStatus.ParentConfirmed // already responded
        });
        db.SaveChanges();

        var service = CreateService(db);

        // Act — coach validates last; triggers auto-advance
        await service.CoachValidateAsync(cls.ClassId, coachUserId, didTeach: true);

        // Assert
        var updated = db.CoachClasses.Find(cls.ClassId)!;
        updated.Status.Should().Be((byte)CoachClassStatus.Pending,
            because: "once the coach validates and all participants have already responded, " +
                     "the class must auto-advance from Finished to Pending");
    }

    // ─── StaffValidateAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task StaffValidateAsync_PendingClass_SetsStatusToValidated()
    {
        // Arrange
        var (db, coach, _, _, modality, studio, parentUser) = SetupBase();
        var cls     = SeedClassWithStatus(db, coach, modality, studio, parentUser,
                          CoachClassStatus.Pending);
        var service = CreateService(db);

        // Act
        await service.StaffValidateAsync(cls.ClassId);

        // Assert
        var updated = db.CoachClasses.Find(cls.ClassId)!;
        updated.Status.Should().Be((byte)CoachClassStatus.Validated,
            because: "staff validating a Pending class must set its status to Validated");
    }

    [Fact]
    public async Task StaffValidateAsync_NotPendingClass_ThrowsInvalidOperation()
    {
        // Arrange — class is Finished, not Pending
        var (db, coach, _, _, modality, studio, parentUser) = SetupBase();
        var cls     = SeedClassWithStatus(db, coach, modality, studio, parentUser,
                          CoachClassStatus.Finished);
        var service = CreateService(db);

        // Act
        Func<Task> act = () => service.StaffValidateAsync(cls.ClassId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>(
            because: "staff validation is only available for Pending classes");
    }
}
