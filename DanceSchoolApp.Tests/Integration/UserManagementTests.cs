using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using DanceSchoolApp.Tests.Helpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace DanceSchoolApp.Tests.Integration;

/// <summary>
/// BPMN 3 — Create New User (integration layer).
/// Covers POST /api/users, PATCH /api/students/{id}/accept, PATCH /api/students/{id}/reject.
/// Happy paths: staff creates user (201), staff accepts/rejects a student (204).
/// Sad  paths: coach cannot create user (403), duplicate username (409).
/// </summary>
[Trait("Category", "Integration")]
public class UserManagementTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public UserManagementTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        // HandleCookies = false: cookies are set manually on every request via
        // MakeRequest so the container cannot override the intended JWT.
        _client  = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies     = false,
            BaseAddress       = new Uri("https://localhost")
        });
    }

    // ─── helpers ─────────────────────────────────────────────────────────────

    private async Task<string> LoginAndGetCookie(string username, string password = "Test1234!")
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new { username, password });

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            because: $"login as '{username}' must succeed before the test steps run");

        var setCookie = response.Headers
            .GetValues("Set-Cookie")
            .FirstOrDefault(h => h.StartsWith("access_token="));

        setCookie.Should().NotBeNull(because: "login must set the access_token cookie");
        return setCookie!.Split(';')[0].Substring("access_token=".Length);
    }

    private static HttpRequestMessage MakeRequest(
        HttpMethod method, string url, string jwt, object? body = null)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Add("Cookie", $"access_token={jwt}");

        if (body is not null)
            req.Content = new StringContent(
                JsonSerializer.Serialize(body),
                Encoding.UTF8,
                "application/json");

        return req;
    }

    // ─── POST /api/users ─────────────────────────────────────────────────────

    [Fact]
    public async Task CreateUser_StaffCaller_Returns201WithUserId()
    {
        // Arrange
        _factory.SeedDatabase(db =>
        {
            SeedData.SeedUserWithRole(db, "staff_cu", "staff");
        });

        var staffJwt = await LoginAndGetCookie("staff_cu");

        // Act — staff creates a new parent user
        var resp = await _client.SendAsync(
            MakeRequest(HttpMethod.Post, "/api/users", staffJwt, new
            {
                username  = "new_parent_um",
                email     = "new_parent@example.com",
                firstRole = 3   // parent
            }));

        // Assert
        resp.StatusCode.Should().Be(HttpStatusCode.Created,
            because: "staff creating a valid user must return 201 Created");

        var body = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("userId").GetInt32()
            .Should().BeGreaterThan(0,
                because: "the response body must contain the new user's positive id");
    }

    [Fact]
    public async Task CreateUser_CoachCaller_Returns403Forbidden()
    {
        // Arrange — coaches are not allowed to create users
        _factory.SeedDatabase(db =>
        {
            var coachUser = SeedData.SeedUserWithRole(db, "coach_cu", "coach");
            SeedData.SeedCoach(db, coachUser);
        });

        var coachJwt = await LoginAndGetCookie("coach_cu");

        // Act
        var resp = await _client.SendAsync(
            MakeRequest(HttpMethod.Post, "/api/users", coachJwt, new
            {
                username  = "should_never_exist",
                email     = "never@example.com",
                firstRole = 3
            }));

        // Assert
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            because: "only staff and admin may create users; a coach must receive 403");
    }

    [Fact]
    public async Task CreateUser_DuplicateUsername_Returns409Conflict()
    {
        // Arrange — seed the target username first
        _factory.SeedDatabase(db =>
        {
            SeedData.SeedUserWithRole(db, "staff_dup", "staff");
            SeedData.SeedUserWithRole(db, "existing_um_user", "parent");
        });

        var staffJwt = await LoginAndGetCookie("staff_dup");

        // Act — try to create another user with the same username
        var resp = await _client.SendAsync(
            MakeRequest(HttpMethod.Post, "/api/users", staffJwt, new
            {
                username  = "existing_um_user",   // already taken
                email     = "unique@example.com",
                firstRole = 3
            }));

        // Assert
        resp.StatusCode.Should().Be(HttpStatusCode.Conflict,
            because: "attempting to create a user with a duplicate username must return 409 Conflict");
    }

    // ─── PATCH /api/students/{id}/accept ─────────────────────────────────────

    [Fact]
    public async Task AcceptStudent_StaffCaller_Returns204()
    {
        // Arrange
        int studentId = 0;
        _factory.SeedDatabase(db =>
        {
            SeedData.SeedUserWithRole(db, "staff_as", "staff");
            var parentUser = SeedData.SeedUserWithRole(db, "parent_as", "parent");
            var student = SeedData.SeedStudent(db, parentUser, "AcceptMe");
            // SeedStudent sets AcceptanceStatus = 1 by default; reset to Pending
            student.AcceptanceStatus = 0;
            db.SaveChanges();
            studentId = student.StudentId;
        });

        var staffJwt = await LoginAndGetCookie("staff_as");

        // Act
        var resp = await _client.SendAsync(
            MakeRequest(HttpMethod.Patch, $"/api/students/{studentId}/accept", staffJwt));

        // Assert
        resp.StatusCode.Should().Be(HttpStatusCode.NoContent,
            because: "staff accepting a pending student must return 204 No Content");
    }

    // ─── PATCH /api/students/{id}/reject ─────────────────────────────────────

    [Fact]
    public async Task RejectStudent_StaffCaller_Returns204()
    {
        // Arrange
        int studentId = 0;
        _factory.SeedDatabase(db =>
        {
            SeedData.SeedUserWithRole(db, "staff_rs", "staff");
            var parentUser = SeedData.SeedUserWithRole(db, "parent_rs", "parent");
            var student = SeedData.SeedStudent(db, parentUser, "RejectMe");
            studentId = student.StudentId;
        });

        var staffJwt = await LoginAndGetCookie("staff_rs");

        // Act
        var resp = await _client.SendAsync(
            MakeRequest(HttpMethod.Patch, $"/api/students/{studentId}/reject", staffJwt,
                new { reason = "Incomplete documents" }));

        // Assert
        resp.StatusCode.Should().Be(HttpStatusCode.NoContent,
            because: "staff rejecting a student must return 204 No Content");
    }
}
