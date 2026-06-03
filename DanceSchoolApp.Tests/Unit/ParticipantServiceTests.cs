using DanceSchoolApp.Server.Data;
using DanceSchoolApp.Server.DTOs.Classes;
using DanceSchoolApp.Server.Models;
using DanceSchoolApp.Server.Services;
using DanceSchoolApp.Server.Services.Classes;
using DanceSchoolApp.Server.Services.Social;
using DanceSchoolApp.Tests.Helpers;
using FluentAssertions;

namespace DanceSchoolApp.Tests.Unit;

/// <summary>
/// BPMN 1 + BPMN 2 — Participant enrollment and attendance validation.
/// Covers JoinClassAsync, ParentValidateAsync, and RemoveParticipantAsync.
/// Happy paths: enroll, parent confirms/disputes, auto-advance to Pending.
/// Sad  paths: class not Approved, class full, wrong parent, already enrolled,
///             already validated, coach not yet validated, last participant blocked.
/// </summary>
[Trait("Category", "Unit")]
public class ParticipantServiceTests
{
    private static ParticipantService CreateService(AppDbContext db) =>
        new ParticipantService(db, new NotificationService(db), new AppSettingService(db));

    private static (AppDbContext db, Coach coach, Modality modality, Studio studio,
                    User parentUser, Student student)
        SetupBase()
    {
        var db = DbContextFactory.Create();
        SeedData.SeedRoles(db);

        var modality = SeedData.SeedModality(db);
        var studio   = SeedData.SeedStudio(db);
        studio.IdModalities.Add(modality);
        db.SaveChanges();

        var coachUser = SeedData.SeedUserWithRole(db, "coach_p", "coach");
        var coach     = SeedData.SeedCoach(db, coachUser);
        coach.IdModalities.Add(modality);
        db.SaveChanges();

        var parentUser = SeedData.SeedUserWithRole(db, "parent_p", "parent");
        var student    = SeedData.SeedStudent(db, parentUser);
        // Assign the student to the modality so enrollment validation passes
        student.IdModalities.Add(modality);
        db.SaveChanges();

        return (db, coach, modality, studio, parentUser, student);
    }

    // ─── JoinClassAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task JoinClassAsync_ValidRequest_CreatesParticipant()
    {
        // Arrange
        var (db, coach, modality, studio, parentUser, student) = SetupBase();
        var cls     = SeedData.SeedCoachClass(db, coach, modality, studio, parentUser,
                          status: (byte)CoachClassStatus.Approved);
        var service = CreateService(db);

        var request = new ParticipantJoinRequest
        {
            ClassId   = cls.ClassId,
            StudentId = student.StudentId
        };

        // Act
        var participantId = await service.JoinClassAsync(request, parentUser.UserId);

        // Assert
        participantId.Should().BeGreaterThan(0,
            because: "a successful enrolment must return a positive participant id");

        var participant = db.Participants.Find(participantId)!;
        participant.IdCoachClass.Should().Be(cls.ClassId);
        participant.IdStudent.Should().Be(student.StudentId);
    }

    [Fact]
    public async Task JoinClassAsync_ClassNotApproved_ThrowsInvalidOperation()
    {
        // Arrange — class is Requested, not Approved
        var (db, coach, modality, studio, parentUser, student) = SetupBase();
        var cls     = SeedData.SeedCoachClass(db, coach, modality, studio, parentUser,
                          status: (byte)CoachClassStatus.Requested);
        var service = CreateService(db);

        Func<Task> act = () => service.JoinClassAsync(
            new ParticipantJoinRequest { ClassId = cls.ClassId, StudentId = student.StudentId },
            parentUser.UserId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>(
            because: "students can only join Approved classes");
    }

    [Fact]
    public async Task JoinClassAsync_ClassFull_ThrowsInvalidOperation()
    {
        // Arrange — capacity explicitly set to 0
        var (db, coach, modality, studio, parentUser, student) = SetupBase();
        var cls = SeedData.SeedCoachClass(db, coach, modality, studio, parentUser,
                      status: (byte)CoachClassStatus.Approved);
        cls.MaxParticipants = 0;
        db.SaveChanges();

        var service = CreateService(db);

        Func<Task> act = () => service.JoinClassAsync(
            new ParticipantJoinRequest { ClassId = cls.ClassId, StudentId = student.StudentId },
            parentUser.UserId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>(
            because: "joining a class that has reached its maximum capacity must be rejected");
    }

    [Fact]
    public async Task JoinClassAsync_WrongParent_ThrowsUnauthorizedAccess()
    {
        // Arrange — a second parent tries to enrol the first parent's student
        var (db, coach, modality, studio, parentUser, student) = SetupBase();
        var cls         = SeedData.SeedCoachClass(db, coach, modality, studio, parentUser,
                              status: (byte)CoachClassStatus.Approved);
        var otherParent = SeedData.SeedUserWithRole(db, "parent2_p", "parent");
        var service     = CreateService(db);

        Func<Task> act = () => service.JoinClassAsync(
            new ParticipantJoinRequest { ClassId = cls.ClassId, StudentId = student.StudentId },
            otherParent.UserId);   // wrong caller

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>(
            because: "a parent may only enrol their own students");
    }

    [Fact]
    public async Task JoinClassAsync_AlreadyEnrolled_ThrowsInvalidOperation()
    {
        // Arrange — student is already in the class
        var (db, coach, modality, studio, parentUser, student) = SetupBase();
        var cls = SeedData.SeedCoachClass(db, coach, modality, studio, parentUser,
                      status: (byte)CoachClassStatus.Approved);

        db.Participants.Add(new Participant
        {
            IdCoachClass     = cls.ClassId,
            IdStudent        = student.StudentId,
            JoinedAt         = DateOnly.FromDateTime(DateTime.UtcNow),
            ValidationStatus = (byte)ParticipantValidationStatus.Pending
        });
        db.SaveChanges();

        var service = CreateService(db);

        Func<Task> act = () => service.JoinClassAsync(
            new ParticipantJoinRequest { ClassId = cls.ClassId, StudentId = student.StudentId },
            parentUser.UserId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>(
            because: "enrolling the same student in the same class a second time must be rejected");
    }

    // ─── ParentValidateAsync ──────────────────────────────────────────────────

    private static (CoachClass cls, Participant participant) SeedFinishedClassWithParticipant(
        AppDbContext db, Coach coach, Modality modality, Studio studio, User parentUser,
        Student student,
        bool coachAlreadyValidated = true,
        byte participantStatus = 0 /* Pending */)
    {
        var cls = SeedData.SeedCoachClass(db, coach, modality, studio, parentUser,
            status: (byte)CoachClassStatus.Finished);

        if (coachAlreadyValidated)
        {
            cls.CoachValidationStatus = (byte)CoachValidationStatus.Confirmed;
            cls.CoachValidatedAt      = DateTime.UtcNow.AddMinutes(-10);
            db.SaveChanges();
        }

        var participant = new Participant
        {
            IdCoachClass     = cls.ClassId,
            IdStudent        = student.StudentId,
            JoinedAt         = DateOnly.FromDateTime(DateTime.UtcNow),
            ValidationStatus = participantStatus
        };
        db.Participants.Add(participant);
        db.SaveChanges();

        return (cls, participant);
    }

    [Fact]
    public async Task ParentValidateAsync_Attended_SetsStatusParentConfirmed()
    {
        // Arrange
        var (db, coach, modality, studio, parentUser, student) = SetupBase();
        var (_, participant) = SeedFinishedClassWithParticipant(db, coach, modality, studio, parentUser, student);
        var service = CreateService(db);

        // Act
        await service.ParentValidateAsync(participant.ParticipantId, attended: true,
            callingUserId: parentUser.UserId);

        // Assert
        var updated = db.Participants.Find(participant.ParticipantId)!;
        updated.ValidationStatus.Should().Be((byte)ParticipantValidationStatus.ParentConfirmed,
            because: "parent confirming attendance must set ValidationStatus to ParentConfirmed");
    }

    [Fact]
    public async Task ParentValidateAsync_NotAttended_SetsStatusDisputed()
    {
        // Arrange
        var (db, coach, modality, studio, parentUser, student) = SetupBase();
        var (_, participant) = SeedFinishedClassWithParticipant(db, coach, modality, studio, parentUser, student);
        var service = CreateService(db);

        // Act
        await service.ParentValidateAsync(participant.ParticipantId, attended: false,
            callingUserId: parentUser.UserId);

        // Assert
        var updated = db.Participants.Find(participant.ParticipantId)!;
        updated.ValidationStatus.Should().Be((byte)ParticipantValidationStatus.Disputed,
            because: "parent disputing attendance must set ValidationStatus to Disputed");
    }

    [Fact]
    public async Task ParentValidateAsync_WrongParent_ThrowsUnauthorizedAccess()
    {
        // Arrange
        var (db, coach, modality, studio, parentUser, student) = SetupBase();
        var (_, participant) = SeedFinishedClassWithParticipant(db, coach, modality, studio, parentUser, student);
        var otherParent = SeedData.SeedUserWithRole(db, "parent2_pv", "parent");
        var service     = CreateService(db);

        Func<Task> act = () => service.ParentValidateAsync(participant.ParticipantId,
            attended: true, callingUserId: otherParent.UserId);

        await act.Should().ThrowAsync<UnauthorizedAccessException>(
            because: "a parent may only validate attendance for their own students");
    }

    [Fact]
    public async Task ParentValidateAsync_AlreadyValidated_ThrowsInvalidOperation()
    {
        // Arrange — participant is already ParentConfirmed (not Pending)
        var (db, coach, modality, studio, parentUser, student) = SetupBase();
        var (_, participant) = SeedFinishedClassWithParticipant(
            db, coach, modality, studio, parentUser, student,
            participantStatus: (byte)ParticipantValidationStatus.ParentConfirmed);
        var service = CreateService(db);

        Func<Task> act = () => service.ParentValidateAsync(participant.ParticipantId,
            attended: true, callingUserId: parentUser.UserId);

        await act.Should().ThrowAsync<InvalidOperationException>(
            because: "a participant record that has already been validated cannot be validated again");
    }

    [Fact]
    public async Task ParentValidateAsync_ClassNotFinished_ThrowsInvalidOperation()
    {
        // Arrange — class is Approved, not Finished
        var (db, coach, modality, studio, parentUser, student) = SetupBase();
        var cls = SeedData.SeedCoachClass(db, coach, modality, studio, parentUser,
                      status: (byte)CoachClassStatus.Approved);
        var participant = new Participant
        {
            IdCoachClass     = cls.ClassId,
            IdStudent        = student.StudentId,
            JoinedAt         = DateOnly.FromDateTime(DateTime.UtcNow),
            ValidationStatus = (byte)ParticipantValidationStatus.Pending
        };
        db.Participants.Add(participant);
        db.SaveChanges();

        var service = CreateService(db);

        Func<Task> act = () => service.ParentValidateAsync(participant.ParticipantId,
            attended: true, callingUserId: parentUser.UserId);

        await act.Should().ThrowAsync<InvalidOperationException>(
            because: "parent validation is only available once the class has Finished");
    }

    [Fact]
    public async Task ParentValidateAsync_CoachNotYetValidated_ThrowsInvalidOperation()
    {
        // Arrange — class is Finished but coach has not validated yet (CoachValidatedAt is null)
        var (db, coach, modality, studio, parentUser, student) = SetupBase();
        var (_, participant) = SeedFinishedClassWithParticipant(
            db, coach, modality, studio, parentUser, student,
            coachAlreadyValidated: false);  // CoachValidatedAt stays null
        var service = CreateService(db);

        Func<Task> act = () => service.ParentValidateAsync(participant.ParticipantId,
            attended: true, callingUserId: parentUser.UserId);

        await act.Should().ThrowAsync<InvalidOperationException>(
            because: "parent validation must wait until the coach has validated first");
    }

    [Fact]
    public async Task ParentValidateAsync_LastPendingResponse_AdvancesClassToPending()
    {
        // Arrange — class is Finished, coach already validated; this parent is the last to respond
        var (db, coach, modality, studio, parentUser, student) = SetupBase();
        var (cls, participant) = SeedFinishedClassWithParticipant(
            db, coach, modality, studio, parentUser, student,
            coachAlreadyValidated: true);
        var service = CreateService(db);

        // Act — parent is the only participant, so their response is the last one
        await service.ParentValidateAsync(participant.ParticipantId,
            attended: true, callingUserId: parentUser.UserId);

        // Assert — all parties responded → class advances to Pending for staff sign-off
        var updatedClass = db.CoachClasses.Find(cls.ClassId)!;
        updatedClass.Status.Should().Be((byte)CoachClassStatus.Pending,
            because: "when the last participant responds and the coach has already validated, " +
                     "the class must auto-advance from Finished to Pending");
    }

    // ─── RemoveParticipantAsync ───────────────────────────────────────────────

    [Fact]
    public async Task RemoveParticipantAsync_LastParticipant_ThrowsInvalidOperation()
    {
        // Arrange — only one participant in an Approved class
        var (db, coach, modality, studio, parentUser, student) = SetupBase();
        var cls = SeedData.SeedCoachClass(db, coach, modality, studio, parentUser,
                      status: (byte)CoachClassStatus.Approved);
        var participant = new Participant
        {
            IdCoachClass     = cls.ClassId,
            IdStudent        = student.StudentId,
            JoinedAt         = DateOnly.FromDateTime(DateTime.UtcNow),
            ValidationStatus = (byte)ParticipantValidationStatus.Pending
        };
        db.Participants.Add(participant);
        db.SaveChanges();

        var service = CreateService(db);

        // Act
        Func<Task> act = () => service.RemoveParticipantAsync(participant.ParticipantId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>(
            because: "removing the last participant from a class is not allowed — cancel the class instead");
    }

    [Fact]
    public async Task RemoveParticipantAsync_FinishedClass_ThrowsInvalidOperation()
    {
        // Arrange — class is Finished; removal would corrupt billing records
        var (db, coach, modality, studio, parentUser, student) = SetupBase();
        var (cls, participant) = SeedFinishedClassWithParticipant(
            db, coach, modality, studio, parentUser, student);

        // Add a second participant so the "last participant" guard is not triggered
        var parentUser2  = SeedData.SeedUserWithRole(db, "parent3_p", "parent");
        var student2     = SeedData.SeedStudent(db, parentUser2);
        db.Participants.Add(new Participant
        {
            IdCoachClass     = cls.ClassId,
            IdStudent        = student2.StudentId,
            JoinedAt         = DateOnly.FromDateTime(DateTime.UtcNow),
            ValidationStatus = (byte)ParticipantValidationStatus.Pending
        });
        db.SaveChanges();

        var service = CreateService(db);

        Func<Task> act = () => service.RemoveParticipantAsync(participant.ParticipantId);

        await act.Should().ThrowAsync<InvalidOperationException>(
            because: "removing a participant from a Finished class would corrupt billing records");
    }
}
