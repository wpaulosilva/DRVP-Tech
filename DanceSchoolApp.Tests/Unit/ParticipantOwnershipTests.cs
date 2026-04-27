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

// NOTE on test 2 (ownership guard):
// ParticipantService.JoinClassAsync currently takes only ParticipantJoinRequest
// (ClassId, StudentId) — there is no callingUserId parameter and
// no ownership check in the method body. The code comment reads:
//   "NOTE: once auth is in, replace request.ParentUserId with the authenticated
//    user id from the JWT claims."
// Test 2 is therefore skipped until JoinClassAsync receives and enforces a
// calling-user identity.  The [Fact(Skip=...)] entry documents the intended
// behaviour so it becomes a failing (and then green) test once implemented.
/*
[Trait("Category", "Unit")]
public class ParticipantOwnershipTests
{
    private static ParticipantService CreateService(AppDbContext db)
    {
        var notifications = new NotificationService(db);
        return new ParticipantService(db, notifications);
    }

    //  1. Happy path 

    [Fact]
    public async Task EnrollStudent_StudentBelongsToCallingParent_Succeeds()
    {
        // Arrange
        var db = DbContextFactory.Create();
        SeedData.SeedRoles(db);
        var modality   = SeedData.SeedModality(db);
        var studio     = SeedData.SeedStudio(db);
        var parentUser = SeedData.SeedUserWithRole(db, "parent1", "parent");
        var coachUser  = SeedData.SeedUserWithRole(db, "coach1",  "coach");
        var coach      = SeedData.SeedCoach(db, coachUser);
        var student    = SeedData.SeedStudent(db, parentUser); // AcceptanceStatus=1

        var coachClass = SeedData.SeedCoachClass(db, coach, modality, studio, coachUser,
            status: (byte)CoachClassStatus.Approved);

        // Ensure there is room (SeedCoachClass defaults to MaxParticipants=10)
        var service = CreateService(db);

        // Act
        var act = async () => await service.JoinClassAsync(new ParticipantJoinRequest
        {
            ClassId   = coachClass.ClassId,
            StudentId = student.StudentId
        }, parentUser.UserId);

        // Assert — no exception, and a Participant row was persisted
        await act.Should().NotThrowAsync();

        bool enrolled = await db.Participants
            .AnyAsync(p => p.IdCoachClass == coachClass.ClassId
                        && p.IdStudent   == student.StudentId);
        enrolled.Should().BeTrue();
    }

    //  2. Ownership guard (not yet implemented) 

    [Fact]
    public async Task EnrollStudent_StudentBelongsToDifferentParent_ThrowsUnauthorizedAccess()
    {
        // Arrange
        var db = DbContextFactory.Create();
        SeedData.SeedRoles(db);
        var modality    = SeedData.SeedModality(db);
        var studio      = SeedData.SeedStudio(db);
        var parentUserA = SeedData.SeedUserWithRole(db, "parentA", "parent");
        var parentUserB = SeedData.SeedUserWithRole(db, "parentB", "parent");
        var coachUser   = SeedData.SeedUserWithRole(db, "coach1",  "coach");
        var coach       = SeedData.SeedCoach(db, coachUser);

        // Student belongs to parent A
        var student = SeedData.SeedStudent(db, parentUserA);

        var coachClass = SeedData.SeedCoachClass(db, coach, modality, studio, coachUser,
            status: (byte)CoachClassStatus.Approved);

        var service = CreateService(db);

        // Act — parent B attempts to enroll parent A's student
        Func<Task> act = () => service.JoinClassAsync(new ParticipantJoinRequest
        {
            ClassId   = coachClass.ClassId,
            StudentId = student.StudentId
        }, parentUserB.UserId);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    //  3. Class capacity 

    [Fact]
    public async Task EnrollStudent_ClassFull_ThrowsInvalidOperation()
    {
        // Arrange
        var db = DbContextFactory.Create();
        SeedData.SeedRoles(db);
        var modality    = SeedData.SeedModality(db);
        var studio      = SeedData.SeedStudio(db);
        var parentUser  = SeedData.SeedUserWithRole(db, "parent1", "parent");
        var coachUser   = SeedData.SeedUserWithRole(db, "coach1",  "coach");
        var coach       = SeedData.SeedCoach(db, coachUser);
        var student1    = SeedData.SeedStudent(db, parentUser, "Ana");
        var student2    = SeedData.SeedStudent(db, parentUser, "Beatriz");

        // Class with a single slot
        var coachClass = SeedData.SeedCoachClass(db, coach, modality, studio, coachUser,
            status: (byte)CoachClassStatus.Approved);

        // Force MaxParticipants=1 so the class is immediately full
        coachClass.MaxParticipants = 1;
        db.SaveChanges();

        // Pre-occupy the only slot
        db.Participants.Add(new Participant
        {
            IdCoachClass     = coachClass.ClassId,
            IdStudent        = student1.StudentId,
            JoinedAt         = DateOnly.FromDateTime(DateTime.UtcNow),

            ValidationStatus = (byte)ParticipantValidationStatus.Pending
        });
        db.SaveChanges();

        var service = CreateService(db);

        // Act — attempt to enrol a second student into an already-full class
        Func<Task> act = () => service.JoinClassAsync(new ParticipantJoinRequest
        {
            ClassId   = coachClass.ClassId,
            StudentId = student2.StudentId
        }, parentUser.UserId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*full*");
    }

    //  4. Class status guard 

    [Fact]
    public async Task EnrollStudent_ClassNotApproved_ThrowsInvalidOperation()
    {
        // Arrange
        var db = DbContextFactory.Create();
        SeedData.SeedRoles(db);
        var modality   = SeedData.SeedModality(db);
        var studio     = SeedData.SeedStudio(db);
        var parentUser = SeedData.SeedUserWithRole(db, "parent1", "parent");
        var coachUser  = SeedData.SeedUserWithRole(db, "coach1",  "coach");
        var coach      = SeedData.SeedCoach(db, coachUser);
        var student    = SeedData.SeedStudent(db, parentUser);

        // Class is still at Requested(0) — not yet open for enrolment
        var coachClass = SeedData.SeedCoachClass(db, coach, modality, studio, coachUser,
            status: (byte)CoachClassStatus.Requested);

        var service = CreateService(db);

        // Act
        Func<Task> act = () => service.JoinClassAsync(new ParticipantJoinRequest
        {
            ClassId   = coachClass.ClassId,
            StudentId = student.StudentId
        }, parentUser.UserId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Approved*");
    }

    //  5. Student acceptance guard 

    [Fact]
    public async Task EnrollStudent_StudentNotAccepted_ThrowsInvalidOperation()
    {
        // Arrange
        var db = DbContextFactory.Create();
        SeedData.SeedRoles(db);
        var modality   = SeedData.SeedModality(db);
        var studio     = SeedData.SeedStudio(db);
        var parentUser = SeedData.SeedUserWithRole(db, "parent1", "parent");
        var coachUser  = SeedData.SeedUserWithRole(db, "coach1",  "coach");
        var coach      = SeedData.SeedCoach(db, coachUser);

        // Build the PersonInfo + Student inline so AcceptanceStatus stays Pending(0)
        var personInfo = new PersonInfo { FirstName = "Carlos", LastName = "Silva", Nif = "384092764" };
        db.PersonInfos.Add(personInfo);
        db.SaveChanges();

        var pendingStudent = new Student
        {
            ParentUserId     = parentUser.UserId,
            PersonInfoId     = personInfo.PersonId,
            IsActive         = true,
            AcceptanceStatus = 0  // Pending — not yet approved by staff
        };
        db.Students.Add(pendingStudent);
        db.SaveChanges();

        var coachClass = SeedData.SeedCoachClass(db, coach, modality, studio, coachUser,
            status: (byte)CoachClassStatus.Approved);

        var service = CreateService(db);

        // Act
        Func<Task> act = () => service.JoinClassAsync(new ParticipantJoinRequest
        {
            ClassId   = coachClass.ClassId,
            StudentId = pendingStudent.StudentId
        }, parentUser.UserId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*accepted*");
    }
}
*/