using DanceSchoolApp.Server.Data;
using DanceSchoolApp.Server.DTOs.Classes;
using DanceSchoolApp.Server.Services.Classes;
using DanceSchoolApp.Server.Services.Social;
using DanceSchoolApp.Tests.Helpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace DanceSchoolApp.Tests.Unit;
/*
[Trait("Category", "Unit")]
public class CoachClassStatusTests
{
    //  Factory helper 

    private static CoachClassService CreateService(AppDbContext db)
    {
        var notifications = new NotificationService(db);
        return new CoachClassService(db, notifications);
    }

    //  StaffApproveAsync 

    [Fact]
    public async Task StaffApproveAsync_FromRequested_TransitionsToStaffApproved()
    {
        // Arrange
        var db = DbContextFactory.Create();
        SeedData.SeedRoles(db);
        var modality   = SeedData.SeedModality(db);
        var studio     = SeedData.SeedStudio(db);
        var coachUser  = SeedData.SeedUserWithRole(db, "coach1", "coach");
        var coach      = SeedData.SeedCoach(db, coachUser);
        var coachClass = SeedData.SeedCoachClass(db, coach, modality, studio, coachUser,
            status: (byte)CoachClassStatus.Requested);

        var service = CreateService(db);

        // Act
        await service.StaffApproveAsync(coachClass.ClassId);

        // Assert
        var reloaded = await db.CoachClasses.AsNoTracking()
            .FirstAsync(c => c.ClassId == coachClass.ClassId);
        reloaded.Status.Should().Be((byte)CoachClassStatus.StaffApproved);
    }

    [Fact]
    public async Task StaffApproveAsync_AlreadyApproved_ThrowsInvalidOperation()
    {
        // Arrange
        var db = DbContextFactory.Create();
        SeedData.SeedRoles(db);
        var modality   = SeedData.SeedModality(db);
        var studio     = SeedData.SeedStudio(db);
        var coachUser  = SeedData.SeedUserWithRole(db, "coach1", "coach");
        var coach      = SeedData.SeedCoach(db, coachUser);
        var coachClass = SeedData.SeedCoachClass(db, coach, modality, studio, coachUser,
            status: (byte)CoachClassStatus.Approved);

        var service = CreateService(db);

        // Act
        Func<Task> act = () => service.StaffApproveAsync(coachClass.ClassId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task StaffApproveAsync_Validated_ThrowsInvalidOperation()
    {
        // Arrange
        var db = DbContextFactory.Create();
        SeedData.SeedRoles(db);
        var modality   = SeedData.SeedModality(db);
        var studio     = SeedData.SeedStudio(db);
        var coachUser  = SeedData.SeedUserWithRole(db, "coach1", "coach");
        var coach      = SeedData.SeedCoach(db, coachUser);
        var coachClass = SeedData.SeedCoachClass(db, coach, modality, studio, coachUser,
            status: (byte)CoachClassStatus.Validated);

        var service = CreateService(db);

        // Act
        Func<Task> act = () => service.StaffApproveAsync(coachClass.ClassId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    //  CoachAcceptAsync 

    [Fact]
    public async Task CoachAcceptAsync_FromStaffApproved_TransitionsToApproved()
    {
        // Arrange
        var db = DbContextFactory.Create();
        SeedData.SeedRoles(db);
        var modality   = SeedData.SeedModality(db);
        var studio     = SeedData.SeedStudio(db);
        var coachUser  = SeedData.SeedUserWithRole(db, "coach1", "coach");
        var coach      = SeedData.SeedCoach(db, coachUser);
        var coachClass = SeedData.SeedCoachClass(db, coach, modality, studio, coachUser,
            status: (byte)CoachClassStatus.StaffApproved);

        var service = CreateService(db);

        // Act
        await service.CoachAcceptAsync(coachClass.ClassId, coachUser.UserId);

        // Assert
        var reloaded = await db.CoachClasses.AsNoTracking()
            .FirstAsync(c => c.ClassId == coachClass.ClassId);
        reloaded.Status.Should().Be((byte)CoachClassStatus.Approved);
    }

    [Fact]
    public async Task CoachAcceptAsync_WrongCoach_ThrowsUnauthorizedAccess()
    {
        // Arrange
        var db = DbContextFactory.Create();
        SeedData.SeedRoles(db);
        var modality   = SeedData.SeedModality(db);
        var studio     = SeedData.SeedStudio(db);
        var coachUser  = SeedData.SeedUserWithRole(db, "coach1", "coach");
        var otherUser  = SeedData.SeedUserWithRole(db, "coach2", "coach");
        var coach      = SeedData.SeedCoach(db, coachUser);
        var coachClass = SeedData.SeedCoachClass(db, coach, modality, studio, coachUser,
            status: (byte)CoachClassStatus.StaffApproved);

        var service = CreateService(db);

        // Act — pass the other coach's id; the class belongs to coachUser
        Func<Task> act = () => service.CoachAcceptAsync(coachClass.ClassId, otherUser.UserId);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task CoachAcceptAsync_FromRequested_ThrowsInvalidOperation()
    {
        // Arrange
        var db = DbContextFactory.Create();
        SeedData.SeedRoles(db);
        var modality   = SeedData.SeedModality(db);
        var studio     = SeedData.SeedStudio(db);
        var coachUser  = SeedData.SeedUserWithRole(db, "coach1", "coach");
        var coach      = SeedData.SeedCoach(db, coachUser);
        var coachClass = SeedData.SeedCoachClass(db, coach, modality, studio, coachUser,
            status: (byte)CoachClassStatus.Requested);

        var service = CreateService(db);

        // Act — class is still Requested, not StaffApproved
        Func<Task> act = () => service.CoachAcceptAsync(coachClass.ClassId, coachUser.UserId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    //  StaffValidateAsync 

    [Fact]
    public async Task StaffValidateAsync_FromPending_TransitionsToValidated()
    {
        // Arrange
        var db = DbContextFactory.Create();
        SeedData.SeedRoles(db);
        var modality   = SeedData.SeedModality(db);
        var studio     = SeedData.SeedStudio(db);
        var coachUser  = SeedData.SeedUserWithRole(db, "coach1", "coach");
        var coach      = SeedData.SeedCoach(db, coachUser);
        var coachClass = SeedData.SeedCoachClass(db, coach, modality, studio, coachUser,
            status: (byte)CoachClassStatus.Pending);

        var service = CreateService(db);

        // Act
        await service.StaffValidateAsync(coachClass.ClassId);

        // Assert
        var reloaded = await db.CoachClasses.AsNoTracking()
            .FirstAsync(c => c.ClassId == coachClass.ClassId);
        reloaded.Status.Should().Be((byte)CoachClassStatus.Validated);
    }

    [Fact]
    public async Task StaffValidateAsync_FromApproved_ThrowsInvalidOperation()
    {
        // Arrange
        var db = DbContextFactory.Create();
        SeedData.SeedRoles(db);
        var modality   = SeedData.SeedModality(db);
        var studio     = SeedData.SeedStudio(db);
        var coachUser  = SeedData.SeedUserWithRole(db, "coach1", "coach");
        var coach      = SeedData.SeedCoach(db, coachUser);
        var coachClass = SeedData.SeedCoachClass(db, coach, modality, studio, coachUser,
            status: (byte)CoachClassStatus.Approved);

        var service = CreateService(db);

        // Act
        Func<Task> act = () => service.StaffValidateAsync(coachClass.ClassId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    //  CancelAsync 

    [Fact]
    public async Task CancelAsync_FromApproved_TransitionsToCancelled()
    {
        // Arrange
        var db = DbContextFactory.Create();
        SeedData.SeedRoles(db);
        var modality   = SeedData.SeedModality(db);
        var studio     = SeedData.SeedStudio(db);
        var coachUser  = SeedData.SeedUserWithRole(db, "coach1", "coach");
        var coach      = SeedData.SeedCoach(db, coachUser);
        var coachClass = SeedData.SeedCoachClass(db, coach, modality, studio, coachUser,
            status: (byte)CoachClassStatus.Approved);

        var service = CreateService(db);

        // Act
        await service.CancelAsync(coachClass.ClassId);

        // Assert
        var reloaded = await db.CoachClasses.AsNoTracking()
            .FirstAsync(c => c.ClassId == coachClass.ClassId);
        reloaded.Status.Should().Be((byte)CoachClassStatus.Cancelled);
    }

    [Fact]
    public async Task CancelAsync_FromValidated_ThrowsInvalidOperation()
    {
        // Arrange
        var db = DbContextFactory.Create();
        SeedData.SeedRoles(db);
        var modality   = SeedData.SeedModality(db);
        var studio     = SeedData.SeedStudio(db);
        var coachUser  = SeedData.SeedUserWithRole(db, "coach1", "coach");
        var coach      = SeedData.SeedCoach(db, coachUser);
        var coachClass = SeedData.SeedCoachClass(db, coach, modality, studio, coachUser,
            status: (byte)CoachClassStatus.Validated);

        var service = CreateService(db);

        // Act
        Func<Task> act = () => service.CancelAsync(coachClass.ClassId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
*/