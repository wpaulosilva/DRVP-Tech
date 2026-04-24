using DanceSchoolApp.Server.DTOs.People;
using DanceSchoolApp.Server.Models;
using DanceSchoolApp.Server.Services;
using DanceSchoolApp.Server.Services.People;
using DanceSchoolApp.Server.Services.Social;
using DanceSchoolApp.Tests.Helpers;
using FluentAssertions;

namespace DanceSchoolApp.Tests.Unit;

[Trait("Category", "Unit")]
public class NifUniquenessTests
{
    // ─── UserService helpers ──────────────────────────────────────────────────

    private static UserService CreateUserService(
        DanceSchoolApp.Server.Data.AppDbContext db)
    {
        var emailService = new EmailService(null!, null!);
        var authService  = new Server.Services.AuthService(db, null!);
        var coachService = new CoachService(db);
        var logger       = new Microsoft.Extensions.Logging.Abstractions
            .NullLogger<UserService>();

        return new UserService(db, emailService, authService, coachService, logger);
    }

    private static StudentService CreateStudentService(
        DanceSchoolApp.Server.Data.AppDbContext db)
    {
        var notifications = new NotificationService(db);
        return new StudentService(db, notifications);
    }

    // ─── Seed a PersonInfo row with a known NIF ───────────────────────────────

    private static void SeedPersonWithNif(
        DanceSchoolApp.Server.Data.AppDbContext db, string nif)
    {
        db.PersonInfos.Add(new PersonInfo
        {
            FirstName = "Existing",
            LastName  = "User",
            Nif       = nif
        });
        db.SaveChanges();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // CreateUser
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CreateUser_DuplicateNif_ThrowsInvalidOperation()
    {
        var db = DbContextFactory.Create();
        SeedData.SeedRoles(db);
        SeedPersonWithNif(db, "123456789");

        var service = CreateUserService(db);

        var act = () => service.CreateUserAsync(new UserCreateRequest
        {
            Username   = "newuser",
            Email      = "new@test.com",
            PersonInfo = new PersonRequest
            {
                FirstName = "New",
                LastName  = "User",
                Nif       = "123456789"
            }
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*NIF*already in use*");
    }

    [Fact]
    public async Task CreateUser_NullNif_DoesNotThrow()
    {
        var db = DbContextFactory.Create();
        SeedData.SeedRoles(db);
        SeedPersonWithNif(db, "123456789");

        var service = CreateUserService(db);

        var act = () => service.CreateUserAsync(new UserCreateRequest
        {
            Username   = "newuser",
            Email      = "new@test.com",
            PersonInfo = new PersonRequest
            {
                FirstName = "New",
                LastName  = "User",
                Nif       = null
            }
        });

        await act.Should().NotThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task CreateUser_UniqueNif_Succeeds()
    {
        var db = DbContextFactory.Create();
        SeedData.SeedRoles(db);
        SeedPersonWithNif(db, "123456789");

        var service = CreateUserService(db);

        var act = () => service.CreateUserAsync(new UserCreateRequest
        {
            Username   = "newuser",
            Email      = "new@test.com",
            PersonInfo = new PersonRequest
            {
                FirstName = "New",
                LastName  = "User",
                Nif       = "987654321"
            }
        });

        await act.Should().NotThrowAsync<InvalidOperationException>();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // UpsertPersonInfo
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task UpsertPersonInfo_DuplicateNif_ThrowsInvalidOperation()
    {
        var db = DbContextFactory.Create();
        SeedData.SeedRoles(db);
        SeedPersonWithNif(db, "111222333");

        // Create a user without NIF
        var user = SeedData.SeedUserWithRole(db, "testuser", "parent");

        var service = CreateUserService(db);

        var act = () => service.UpsertPersonInfoAsync(user.UserId,
            new UpdatePersonInfoRequest
            {
                FirstName = "Test",
                Nif       = "111222333"
            });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*NIF*already in use*");
    }

    [Fact]
    public async Task UpsertPersonInfo_SameNifSamePerson_DoesNotThrow()
    {
        var db = DbContextFactory.Create();
        SeedData.SeedRoles(db);

        // Create a user with PersonInfo that already has a NIF
        var user = new User
        {
            Username     = "testuser",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Test1234!"),
            IsActive     = true,
            CreatedAt    = DateOnly.FromDateTime(DateTime.UtcNow),
            PersonInfo   = new PersonInfo
            {
                FirstName = "Test",
                LastName  = "User",
                Nif       = "444555666"
            }
        };
        db.Users.Add(user);
        db.SaveChanges();

        var service = CreateUserService(db);

        // Updating the same user with the same NIF should not throw
        var act = () => service.UpsertPersonInfoAsync(user.UserId,
            new UpdatePersonInfoRequest
            {
                FirstName = "Updated",
                Nif       = "444555666"
            });

        await act.Should().NotThrowAsync<InvalidOperationException>();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // CreateStudent
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CreateStudent_DuplicateNif_ThrowsInvalidOperation()
    {
        var db = DbContextFactory.Create();
        SeedData.SeedRoles(db);
        SeedPersonWithNif(db, "999888777");

        var parentUser = SeedData.SeedUserWithRole(db, "parent1", "parent");
        var service    = CreateStudentService(db);

        var act = () => service.CreateStudentAsync(new StudentCreateRequest
        {
            ParentId  = parentUser.UserId,
            FirstName = "Child",
            LastName  = "Test",
            Nif       = "999888777"
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*NIF*already in use*");
    }

    [Fact]
    public async Task CreateStudent_NullNif_DoesNotThrow()
    {
        var db = DbContextFactory.Create();
        SeedData.SeedRoles(db);
        SeedPersonWithNif(db, "999888777");

        var parentUser = SeedData.SeedUserWithRole(db, "parent1", "parent");
        var service    = CreateStudentService(db);

        var act = () => service.CreateStudentAsync(new StudentCreateRequest
        {
            ParentId  = parentUser.UserId,
            FirstName = "Child",
            LastName  = "Test",
            Nif       = null
        });

        await act.Should().NotThrowAsync<InvalidOperationException>();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // UpdateStudent
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task UpdateStudent_DuplicateNif_ThrowsInvalidOperation()
    {
        var db = DbContextFactory.Create();
        SeedData.SeedRoles(db);
        SeedPersonWithNif(db, "555666777");

        var parentUser = SeedData.SeedUserWithRole(db, "parent1", "parent");
        var student    = SeedData.SeedStudent(db, parentUser);
        var service    = CreateStudentService(db);

        var act = () => service.UpdateStudentAsync(student.StudentId,
            new StudentUpdateRequest
            {
                FirstName = "Ana",
                LastName  = "Silva",
                Nif       = "555666777"
            });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*NIF*already in use*");
    }

    [Fact]
    public async Task UpdateStudent_SameNifSameStudent_DoesNotThrow()
    {
        var db = DbContextFactory.Create();
        SeedData.SeedRoles(db);

        var parentUser = SeedData.SeedUserWithRole(db, "parent1", "parent");
        // Create student with a known NIF
        var personInfo = new PersonInfo
        {
            FirstName = "Ana",
            LastName  = "Silva",
            Nif       = "111111111"
        };
        db.PersonInfos.Add(personInfo);
        db.SaveChanges();

        var student = new Student
        {
            ParentUserId     = parentUser.UserId,
            PersonInfoId     = personInfo.PersonId,
            IsActive         = true,
            AcceptanceStatus = 1
        };
        db.Students.Add(student);
        db.SaveChanges();

        var service = CreateStudentService(db);

        // Updating with the same NIF should not throw
        var act = () => service.UpdateStudentAsync(student.StudentId,
            new StudentUpdateRequest
            {
                FirstName = "Ana Updated",
                LastName  = "Silva",
                Nif       = "111111111"
            });

        await act.Should().NotThrowAsync<InvalidOperationException>();
    }
}
