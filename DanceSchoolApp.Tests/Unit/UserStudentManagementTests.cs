using DanceSchoolApp.Server.Data;
using DanceSchoolApp.Server.DTOs.People;
using DanceSchoolApp.Server.Models;
using DanceSchoolApp.Server.Services.People;
using DanceSchoolApp.Server.Services.Social;
using DanceSchoolApp.Tests.Helpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace DanceSchoolApp.Tests.Unit;

/// <summary>
/// BPMN 3 — Create New User / Manage Students (unit layer).
/// Covers UserService.CreateUserAsync and StudentService acceptance workflow.
/// Happy paths: create user, accept/reject student, update resets status.
/// Sad  paths: duplicate username, duplicate email, future birth date,
///             duplicate NIF, already accepted student.
/// </summary>
[Trait("Category", "Unit")]
public class UserStudentManagementTests
{
    // ─── Factories ────────────────────────────────────────────────────────────

    private static UserService CreateUserService(AppDbContext db) =>
        new UserService(
            db,
            emailService:   null!,    // inside try-catch — NullReferenceException is caught
            authService:    null!,    // inside try-catch — NullReferenceException is caught
            coachService:   new CoachService(db),
            logger:         NullLogger<UserService>.Instance,
            config:         null!);   // FrontendBaseUrl only needed by email path, which is null! above

    private static StudentService CreateStudentService(AppDbContext db) =>
        new StudentService(db, new NotificationService(db));

    private static AppDbContext SetupBase()
    {
        var db = DbContextFactory.Create();
        SeedData.SeedRoles(db);
        return db;
    }

    // ─── UserService.CreateUserAsync ──────────────────────────────────────────

    [Fact]
    public async Task CreateUserAsync_ValidRequest_ReturnsNewUserId()
    {
        // Arrange
        var db      = SetupBase();
        var service = CreateUserService(db);

        var request = new UserCreateRequest
        {
            Username  = "new_staff_user",
            Email     = "staff@example.com",
            FirstRole = 1 // staff
        };

        // Act
        var userId = await service.CreateUserAsync(request);

        // Assert
        userId.Should().BeGreaterThan(0,
            because: "a successful user creation must return a positive user id");

        var user = db.Users.Find(userId)!;
        user.Username.Should().Be("new_staff_user");
        user.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task CreateUserAsync_DuplicateUsername_ThrowsInvalidOperation()
    {
        // Arrange — seed a user with the same username
        var db = SetupBase();
        SeedData.SeedUserWithRole(db, "duplicate_user", "staff");
        var service = CreateUserService(db);

        var request = new UserCreateRequest
        {
            Username  = "duplicate_user",
            Email     = "other@example.com",
            FirstRole = 1
        };

        // Act
        Func<Task> act = () => service.CreateUserAsync(request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>(
            because: "creating a user with an already-taken username must be rejected");
    }

    [Fact]
    public async Task CreateUserAsync_DuplicateEmail_ThrowsInvalidOperation()
    {
        // Arrange — seed a user with the same email address
        var db       = SetupBase();
        var existing = SeedData.SeedUserWithRole(db, "first_user", "staff");
        existing.Email = "shared@example.com";
        db.SaveChanges();

        var service = CreateUserService(db);

        var request = new UserCreateRequest
        {
            Username  = "second_user",
            Email     = "shared@example.com",  // same email
            FirstRole = 1
        };

        Func<Task> act = () => service.CreateUserAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>(
            because: "creating a user with an already-registered email must be rejected");
    }

    [Fact]
    public async Task CreateUserAsync_FutureBirthDate_ThrowsInvalidOperation()
    {
        // Arrange
        var db      = SetupBase();
        var service = CreateUserService(db);

        var request = new UserCreateRequest
        {
            Username   = "future_user",
            Email      = "future@example.com",
            FirstRole  = 1,
            PersonInfo = new PersonRequest
            {
                FirstName = "Test",
                LastName  = "User",
                BirthDate = DateOnly.FromDateTime(DateTime.Now.AddYears(1)) // future date
            }
        };

        Func<Task> act = () => service.CreateUserAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>(
            because: "a birth date in the future is not valid and must be rejected");
    }

    [Fact]
    public async Task CreateUserAsync_DuplicateNif_ThrowsInvalidOperation()
    {
        // Arrange — PersonInfo with a NIF already in use
        var db = SetupBase();

        // Seed an existing person with the NIF
        var personInfo = new PersonInfo { FirstName = "Existing", LastName = "Person", Nif = "123456789" };
        db.PersonInfos.Add(personInfo);
        db.SaveChanges();

        var service = CreateUserService(db);

        var request = new UserCreateRequest
        {
            Username   = "nif_conflict_user",
            Email      = "nif@example.com",
            FirstRole  = 1,
            PersonInfo = new PersonRequest
            {
                FirstName = "New",
                LastName  = "User",
                BirthDate = DateOnly.FromDateTime(DateTime.Now.AddYears(-20)),
                Nif       = "123456789"   // same NIF
            }
        };

        Func<Task> act = () => service.CreateUserAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>(
            because: "creating a user with a NIF already in use must be rejected");
    }

    // ─── StudentService.AcceptStudentAsync ────────────────────────────────────

    [Fact]
    public async Task AcceptStudentAsync_PendingStudent_SetsStatusAccepted()
    {
        // Arrange
        var db         = SetupBase();
        var parentUser = SeedData.SeedUserWithRole(db, "parent_accept", "parent");
        var student    = SeedData.SeedStudent(db, parentUser);

        // SeedStudent sets AcceptanceStatus = 1 (Accepted) by default — reset to Pending
        student.AcceptanceStatus = (byte)StudentAcceptanceStatus.Pending;
        db.SaveChanges();

        var service = CreateStudentService(db);

        // Act
        await service.AcceptStudentAsync(student.StudentId);

        // Assert
        var updated = db.Students.Find(student.StudentId)!;
        updated.AcceptanceStatus.Should().Be((byte)StudentAcceptanceStatus.Accepted,
            because: "accepting a pending student must set AcceptanceStatus to Accepted");
    }

    [Fact]
    public async Task AcceptStudentAsync_AlreadyAccepted_ThrowsInvalidOperation()
    {
        // Arrange — student is already Accepted
        var db         = SetupBase();
        var parentUser = SeedData.SeedUserWithRole(db, "parent_accept2", "parent");
        var student    = SeedData.SeedStudent(db, parentUser);  // AcceptanceStatus = 1

        var service = CreateStudentService(db);

        // Act
        Func<Task> act = () => service.AcceptStudentAsync(student.StudentId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>(
            because: "accepting a student who is already accepted must be rejected");
    }

    // ─── StudentService.RejectStudentAsync ───────────────────────────────────

    [Fact]
    public async Task RejectStudentAsync_AnyStudent_SetsStatusRejected()
    {
        // Arrange
        var db         = SetupBase();
        var parentUser = SeedData.SeedUserWithRole(db, "parent_reject", "parent");
        var student    = SeedData.SeedStudent(db, parentUser);
        var service    = CreateStudentService(db);

        // Act
        await service.RejectStudentAsync(student.StudentId, reason: "Incomplete data");

        // Assert
        var updated = db.Students.Find(student.StudentId)!;
        updated.AcceptanceStatus.Should().Be((byte)StudentAcceptanceStatus.Rejected,
            because: "rejecting a student must set AcceptanceStatus to Rejected");
    }

    // ─── StudentService.UpdateStudentAsync ───────────────────────────────────

    [Fact]
    public async Task UpdateStudentAsync_ResetsAcceptanceStatusToPending()
    {
        // Arrange — student starts as Accepted
        var db         = SetupBase();
        var parentUser = SeedData.SeedUserWithRole(db, "parent_update", "parent");
        var student    = SeedData.SeedStudent(db, parentUser);  // AcceptanceStatus = 1 (Accepted)
        var service    = CreateStudentService(db);

        var updateRequest = new StudentUpdateRequest
        {
            FirstName = "UpdatedName",
            LastName  = "UpdatedLast",
            Nif       = "123456789"
        };

        // Act
        await service.UpdateStudentAsync(student.StudentId, updateRequest);

        // Assert — parent editing data forces staff to re-review
        var updated = db.Students.Find(student.StudentId)!;
        updated.AcceptanceStatus.Should().Be((byte)StudentAcceptanceStatus.Pending,
            because: "when a parent updates student data, AcceptanceStatus must reset to Pending " +
                     "so staff can re-review the changes");
    }

    // ─── StudentService.CreateStudentAsync ───────────────────────────────────

    [Fact]
    public async Task CreateStudentAsync_DuplicateNif_ThrowsInvalidOperation()
    {
        // Arrange — seed a PersonInfo with the target NIF already
        var db = SetupBase();
        db.PersonInfos.Add(new PersonInfo
            { FirstName = "First", LastName = "Student", Nif = "999999999" });
        db.SaveChanges();

        var parentUser = SeedData.SeedUserWithRole(db, "parent_nif", "parent");
        var service    = CreateStudentService(db);

        var request = new StudentCreateRequest
        {
            ParentId  = parentUser.UserId,
            FirstName = "Second",
            LastName  = "Student",
            Nif       = "999999999"   // same NIF
        };

        Func<Task> act = () => service.CreateStudentAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>(
            because: "creating a student with a NIF already in use must be rejected");
    }
}
