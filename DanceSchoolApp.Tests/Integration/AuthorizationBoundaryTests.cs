using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using DanceSchoolApp.Server.DTOs.Classes;
using DanceSchoolApp.Tests.Helpers;

namespace DanceSchoolApp.Tests.Integration;
/*
[Trait("Category", "Integration")]
public class AuthorizationBoundaryTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AuthorizationBoundaryTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
            BaseAddress = new Uri("https://localhost")
        });
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private async Task<string> LoginAndGetCookie(string username, string password = "Test1234!")
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new { username, password });

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            because: $"login as '{username}' must succeed before the boundary assertion");

        var setCookie = response.Headers
            .GetValues("Set-Cookie")
            .FirstOrDefault(h => h.StartsWith("jwt="));

        setCookie.Should().NotBeNull(because: "a successful login must issue the jwt cookie");
        return setCookie!.Split(';')[0].Substring("jwt=".Length);
    }

    private static HttpRequestMessage MakeRequest(
        HttpMethod method, string url, string jwt, object? body = null)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Add("Cookie", $"jwt={jwt}");

        if (body is not null)
        {
            req.Content = new StringContent(
                JsonSerializer.Serialize(body),
                Encoding.UTF8,
                "application/json");
        }

        return req;
    }

    // ── tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task StaffOnlyEndpoint_CalledByParent_Returns403()
    {
        // Arrange — only a parent exists; no class data needed because the
        // [Authorize(Roles = "staff")] attribute rejects the request before
        // the controller body (and therefore any DB query) runs.
        _factory.SeedDatabase(db =>
        {
            SeedData.SeedUserWithRole(db, "parent_bnd1", "parent");
        });
        var parentJwt = await LoginAndGetCookie("parent_bnd1");

        // Act
        var response = await _client.SendAsync(
            MakeRequest(HttpMethod.Get, "/api/coachclasses", parentJwt));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            because: "GET /api/coachclasses is [Authorize(Roles=\"staff\")] — an " +
                     "authenticated parent must receive 403, not 401 or 200");
    }

    [Fact]
    public async Task StaffApprove_CalledByParent_Returns403()
    {
        // Arrange — seed a real Requested class so the route resolves; the 403
        // fires from the [Authorize(Roles = "staff")] attribute before the
        // service is ever invoked.
        int classId = 0;
        _factory.SeedDatabase(db =>
        {
            var staff     = SeedData.SeedUserWithRole(db, "staff_bnd2",  "staff");
            var coachUser = SeedData.SeedUserWithRole(db, "coach_bnd2",  "coach");
            var coach     = SeedData.SeedCoach(db, coachUser);
                            SeedData.SeedUserWithRole(db, "parent_bnd2", "parent");
            var modality  = SeedData.SeedModality(db, "Modal BND2");
            var studio    = SeedData.SeedStudio(db,   "Studio BND2");

            // Insert the class directly — no HTTP, no studio↔modality join needed.
            var cls = SeedData.SeedCoachClass(
                db, coach, modality, studio, createdByUser: staff,
                status: (byte)CoachClassStatus.Requested);
            classId = cls.ClassId;
        });

        var parentJwt = await LoginAndGetCookie("parent_bnd2");

        // Act
        var response = await _client.SendAsync(
            MakeRequest(HttpMethod.Patch, $"/api/coachclasses/{classId}/staff-approve", parentJwt));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            because: "PATCH staff-approve is [Authorize(Roles=\"staff\")] — " +
                     "a parent must be rejected with 403");
    }

    [Fact]
    public async Task CoachAccept_CalledByStaff_Returns403()
    {
        // Arrange — seed a StaffApproved class; the 403 comes from
        // [Authorize(Roles = "coach")] before any service logic runs.
        int classId = 0;
        _factory.SeedDatabase(db =>
        {
            var staff     = SeedData.SeedUserWithRole(db, "staff_bnd3",  "staff");
            var coachUser = SeedData.SeedUserWithRole(db, "coach_bnd3",  "coach");
            var coach     = SeedData.SeedCoach(db, coachUser);
            var modality  = SeedData.SeedModality(db, "Modal BND3");
            var studio    = SeedData.SeedStudio(db,   "Studio BND3");

            var cls = SeedData.SeedCoachClass(
                db, coach, modality, studio, createdByUser: staff,
                status: (byte)CoachClassStatus.StaffApproved);
            classId = cls.ClassId;
        });

        var staffJwt = await LoginAndGetCookie("staff_bnd3");

        // Act
        var response = await _client.SendAsync(
            MakeRequest(HttpMethod.Patch, $"/api/coachclasses/{classId}/coach-accept", staffJwt));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            because: "PATCH coach-accept is [Authorize(Roles=\"coach\")] — " +
                     "an authenticated staff user must receive 403");
    }

    [Fact]
    public async Task ParentCannotAccessAnotherParentsStudent()
    {
        // Arrange — the endpoint allows both staff and parent, but the controller
        // explicitly calls Forbid() when result.ParentUserId != caller's userId.
        // The student must actually exist so the service doesn't return 404 first.
        int studentId = 0;
        _factory.SeedDatabase(db =>
        {
            var parentA = SeedData.SeedUserWithRole(db, "parentA_bnd4", "parent");
                          SeedData.SeedUserWithRole(db, "parentB_bnd4", "parent");

            var student = SeedData.SeedStudent(db, parentA, "Beatriz");
            studentId = student.StudentId;
        });

        // Parent B has a valid, authenticated session but owns no students.
        var parentBJwt = await LoginAndGetCookie("parentB_bnd4");

        // Act
        var response = await _client.SendAsync(
            MakeRequest(HttpMethod.Get, $"/api/students/{studentId}", parentBJwt));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            because: "GET /api/students/{id} returns Forbid() when the " +
                     "calling parent is not the student's owner");
    }

    [Fact]
    public async Task UnauthenticatedRequest_Returns401()
    {
        // Act — each test class instance constructs a fresh HttpClient with an
        // empty cookie jar; there is no prior login in this test so no jwt cookie
        // is attached.
        var response = await _client.GetAsync("/api/coachclasses");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            because: "any [Authorize] endpoint must return 401 when no jwt cookie is present");
    }
}
*/