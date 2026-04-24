using DanceSchoolApp.Server.Data;
using DanceSchoolApp.Server.DTOs.Classes;
using DanceSchoolApp.Server.Models;
using DanceSchoolApp.Server.Services;
using DanceSchoolApp.Server.Services.Classes;
using DanceSchoolApp.Server.Services.Social;
using DanceSchoolApp.Tests.Helpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace DanceSchoolApp.Tests.Unit;

[Trait("Category", "Unit")]
public class ParentValidationTests
{
    private static ParticipantService CreateService(AppDbContext db)
    {
        var notifications = new NotificationService(db);
        var appSettings   = new AppSettingService(db);
        return new ParticipantService(db, notifications, appSettings);
    }

    /// <summary>
    /// Seeds a complete class + participant scenario with coach validation state.
    /// </summary>
    private static (int participantId, int classId) SeedFinishedClassWithParticipant(
        AppDbContext db,
        byte classStatus,
        DateTime? coachValidatedAt)
    {
        SeedData.SeedRoles(db);
        SeedData.SeedAppSettings(db);
        var modality   = SeedData.SeedModality(db);
        var studio     = SeedData.SeedStudio(db);
        var parentUser = SeedData.SeedUserWithRole(db, "parent1", "parent");
        var coachUser  = SeedData.SeedUserWithRole(db, "coach1", "coach");
        var coach      = SeedData.SeedCoach(db, coachUser);
        var student    = SeedData.SeedStudent(db, parentUser);

        var start = new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var coachClass = SeedData.SeedCoachClass(db, coach, modality, studio, coachUser,
            start: start, durationMinutes: 60, status: classStatus);

        coachClass.CoachValidatedAt       = coachValidatedAt;
        coachClass.CoachValidationStatus  = coachValidatedAt.HasValue
            ? (byte)1  // Confirmed
            : (byte)0; // Pending
        db.SaveChanges();

        var participant = new Participant
        {
            IdCoachClass     = coachClass.ClassId,
            IdStudent        = student.StudentId,
            JoinedAt         = DateOnly.FromDateTime(start),
            ValidationStatus = (byte)ParticipantValidationStatus.Pending,
            ClassPrice       = 36.00m
        };
        db.Participants.Add(participant);
        db.SaveChanges();

        // Seed staff users so notification code doesn't fail
        SeedData.SeedUserWithRole(db, "staff1", "staff");

        return (participant.ParticipantId, coachClass.ClassId);
    }

    // ─── Coach must validate first ───────────────────────────────────────────

    [Fact]
    public async Task ParentValidate_CoachNotYetValidated_ThrowsInvalidOperation()
    {
        var db = DbContextFactory.Create();
        var (participantId, _) = SeedFinishedClassWithParticipant(
            db,
            classStatus: (byte)CoachClassStatus.Finished,
            coachValidatedAt: null);

        var service = CreateService(db);

        var act = () => service.ParentValidateAsync(participantId, attended: true);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*coach validates*");
    }

    [Fact]
    public async Task ParentValidate_CoachValidated_FinishedClass_Succeeds()
    {
        var db = DbContextFactory.Create();
        var coachValidatedAt = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var (participantId, _) = SeedFinishedClassWithParticipant(
            db,
            classStatus: (byte)CoachClassStatus.Finished,
            coachValidatedAt: coachValidatedAt);

        var service = CreateService(db);

        var act = () => service.ParentValidateAsync(participantId, attended: true);

        await act.Should().NotThrowAsync();

        var participant = await db.Participants.FindAsync(participantId);
        participant!.ValidationStatus.Should().Be(
            (byte)ParticipantValidationStatus.ParentConfirmed);
        participant.ParentValidatedAt.Should().NotBeNull();
    }

    // ─── Parent can validate during Pending (past-due) ───────────────────────

    [Fact]
    public async Task ParentValidate_PendingClass_CoachValidated_Succeeds()
    {
        var db = DbContextFactory.Create();
        var coachValidatedAt = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var (participantId, _) = SeedFinishedClassWithParticipant(
            db,
            classStatus: (byte)CoachClassStatus.Pending,
            coachValidatedAt: coachValidatedAt);

        var service = CreateService(db);

        var act = () => service.ParentValidateAsync(participantId, attended: true);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ParentValidate_PendingClass_NotifiesStaff()
    {
        var db = DbContextFactory.Create();
        var coachValidatedAt = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var (participantId, classId) = SeedFinishedClassWithParticipant(
            db,
            classStatus: (byte)CoachClassStatus.Pending,
            coachValidatedAt: coachValidatedAt);

        var service = CreateService(db);

        await service.ParentValidateAsync(participantId, attended: true);

        // Should have created a "Past-Due" notification for staff
        var staffNotifications = await db.Notifications
            .Where(n => n.Title == "Past-Due Parent Validation"
                     && n.EntityId == classId)
            .ToListAsync();

        staffNotifications.Should().NotBeEmpty();
    }

    // ─── Status guard ────────────────────────────────────────────────────────

    [Fact]
    public async Task ParentValidate_ApprovedClass_ThrowsInvalidOperation()
    {
        var db = DbContextFactory.Create();
        var (participantId, _) = SeedFinishedClassWithParticipant(
            db,
            classStatus: (byte)CoachClassStatus.Approved,
            coachValidatedAt: DateTime.UtcNow);

        var service = CreateService(db);

        var act = () => service.ParentValidateAsync(participantId, attended: true);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Finished or Pending*");
    }

    // ─── Dispute path ────────────────────────────────────────────────────────

    [Fact]
    public async Task ParentValidate_Disputed_SetsCorrectStatus()
    {
        var db = DbContextFactory.Create();
        var coachValidatedAt = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var (participantId, _) = SeedFinishedClassWithParticipant(
            db,
            classStatus: (byte)CoachClassStatus.Finished,
            coachValidatedAt: coachValidatedAt);

        var service = CreateService(db);

        await service.ParentValidateAsync(participantId, attended: false);

        var participant = await db.Participants.FindAsync(participantId);
        participant!.ValidationStatus.Should().Be(
            (byte)ParticipantValidationStatus.Disputed);
    }

    // ─── Re-validation guard ─────────────────────────────────────────────────

    [Fact]
    public async Task ParentValidate_AlreadyValidated_ThrowsInvalidOperation()
    {
        var db = DbContextFactory.Create();
        var coachValidatedAt = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var (participantId, _) = SeedFinishedClassWithParticipant(
            db,
            classStatus: (byte)CoachClassStatus.Finished,
            coachValidatedAt: coachValidatedAt);

        var service = CreateService(db);

        // First validation
        await service.ParentValidateAsync(participantId, attended: true);

        // Second attempt
        var act = () => service.ParentValidateAsync(participantId, attended: true);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already been validated*");
    }
}
