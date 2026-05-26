using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using DanceSchoolApp.Server.DTOs.Classes;
using DanceSchoolApp.Tests.Helpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace DanceSchoolApp.Tests.Integration;

/// <summary>
/// BPMN 1 — Create / Enroll in Classes (integration / sad paths).
/// Verifies that the HTTP endpoints correctly enforce BPMN rules:
///   - Staff rejects a class request → 204 and status Rejected
///   - Coach rejects a staff-approved class → 204 and status Rejected
///   - Staff cancels an Approved class → 204 and status Cancelled
///   - Wrong role on staff-respond → 403 Forbidden
/// </summary>
[Trait("Category", "Integration")]
public class ClassApprovalFlowTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public ClassApprovalFlowTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        // HandleCookies = false: all authentication is managed manually via
        // MakeRequest (explicit Cookie header), so we don't want the HttpClient's
        // cookie container to merge stored cookies and send the wrong JWT.
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

    private async Task<int> GetClassStatus(int classId, string jwt)
    {
        var resp = await _client.SendAsync(
            MakeRequest(HttpMethod.Get, $"/api/coachclasses/{classId}", jwt));
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("status").GetInt32();
    }

    // ─── tests ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task StaffRespond_Reject_Returns204AndClassIsRejected()
    {
        // Arrange — seed a CoachApproved class (staff acts after the coach)
        int classId = 0;
        _factory.SeedDatabase(db =>
        {
            SeedData.SeedUserWithRole(db, "staff_rej", "staff");
            var coachUser = SeedData.SeedUserWithRole(db, "coach_rej", "coach");
            var coach     = SeedData.SeedCoach(db, coachUser);
            var modality  = SeedData.SeedModality(db, "Jazz Rej");
            var studio    = SeedData.SeedStudio(db, "Studio Rej");
            studio.IdModalities.Add(modality);
            coach.IdModalities.Add(modality);
            db.SaveChanges();

            var parentUser = SeedData.SeedUserWithRole(db, "parent_rej", "parent");
            var cls = SeedData.SeedCoachClass(db, coach, modality, studio, parentUser,
                          status: (byte)CoachClassStatus.CoachApproved);
            classId = cls.ClassId;
        });

        var staffJwt = await LoginAndGetCookie("staff_rej");

        // Act — staff rejects the request
        var resp = await _client.SendAsync(
            MakeRequest(HttpMethod.Patch, $"/api/coachclasses/{classId}/staff-respond", staffJwt,
                new { approve = false, reason = "Room unavailable" }));

        // Assert
        resp.StatusCode.Should().Be(HttpStatusCode.NoContent,
            because: "staff-respond (approve=false) on a Requested class must return 204");

        var status = await GetClassStatus(classId, staffJwt);
        status.Should().Be((int)CoachClassStatus.Rejected,
            because: "after staff rejects, the class status must be Rejected");
    }

    [Fact]
    public async Task CoachRespond_Reject_Returns204AndClassIsRejected()
    {
        // Arrange — seed a Requested class (coach acts first now)
        int classId = 0;
        _factory.SeedDatabase(db =>
        {
            SeedData.SeedUserWithRole(db, "staff_cr", "staff");
            var coachUser = SeedData.SeedUserWithRole(db, "coach_cr", "coach");
            var coach     = SeedData.SeedCoach(db, coachUser);
            var modality  = SeedData.SeedModality(db, "Jazz CR");
            var studio    = SeedData.SeedStudio(db, "Studio CR");
            studio.IdModalities.Add(modality);
            coach.IdModalities.Add(modality);
            db.SaveChanges();

            var parentUser = SeedData.SeedUserWithRole(db, "parent_cr", "parent");
            var cls = SeedData.SeedCoachClass(db, coach, modality, studio, parentUser,
                          status: (byte)CoachClassStatus.Requested);
            classId = cls.ClassId;
        });

        var coachJwt = await LoginAndGetCookie("coach_cr");
        var staffJwt = await LoginAndGetCookie("staff_cr");

        // Act — coach rejects the staff-approved class
        var resp = await _client.SendAsync(
            MakeRequest(HttpMethod.Patch, $"/api/coachclasses/{classId}/coach-respond", coachJwt,
                new { accept = false, reason = "Schedule conflict" }));

        // Assert
        resp.StatusCode.Should().Be(HttpStatusCode.NoContent,
            because: "coach-respond (accept=false) on a Requested class must return 204");

        var status = await GetClassStatus(classId, staffJwt);
        status.Should().Be((int)CoachClassStatus.Rejected,
            because: "after coach rejects, the class status must be Rejected");
    }

    [Fact]
    public async Task CancelApprovedClass_Returns204AndClassIsCancelled()
    {
        // Arrange — seed an Approved class directly
        int classId = 0;
        _factory.SeedDatabase(db =>
        {
            SeedData.SeedUserWithRole(db, "staff_can", "staff");
            var coachUser = SeedData.SeedUserWithRole(db, "coach_can", "coach");
            var coach     = SeedData.SeedCoach(db, coachUser);
            var modality  = SeedData.SeedModality(db, "Jazz Can");
            var studio    = SeedData.SeedStudio(db, "Studio Can");
            studio.IdModalities.Add(modality);
            coach.IdModalities.Add(modality);
            db.SaveChanges();

            var parentUser = SeedData.SeedUserWithRole(db, "parent_can", "parent");
            var cls = SeedData.SeedCoachClass(db, coach, modality, studio, parentUser,
                          status: (byte)CoachClassStatus.Approved);
            classId = cls.ClassId;
        });

        var staffJwt = await LoginAndGetCookie("staff_can");

        // Act — staff cancels the Approved class
        var resp = await _client.SendAsync(
            MakeRequest(HttpMethod.Patch, $"/api/coachclasses/{classId}/cancel", staffJwt));

        // Assert
        resp.StatusCode.Should().Be(HttpStatusCode.NoContent,
            because: "cancelling an Approved class must return 204");

        var status = await GetClassStatus(classId, staffJwt);
        status.Should().Be((int)CoachClassStatus.Cancelled,
            because: "after cancellation, the class status must be Cancelled");
    }

    [Fact]
    public async Task StaffRespond_CalledByParent_Returns403Forbidden()
    {
        // Arrange — seed a Requested class; parent tries to call staff-respond
        int classId = 0;
        _factory.SeedDatabase(db =>
        {
            var coachUser = SeedData.SeedUserWithRole(db, "coach_403", "coach");
            var coach     = SeedData.SeedCoach(db, coachUser);
            var modality  = SeedData.SeedModality(db, "Jazz 403");
            var studio    = SeedData.SeedStudio(db, "Studio 403");
            studio.IdModalities.Add(modality);
            coach.IdModalities.Add(modality);
            db.SaveChanges();

            var parentUser = SeedData.SeedUserWithRole(db, "parent_403", "parent");
            var cls = SeedData.SeedCoachClass(db, coach, modality, studio, parentUser,
                          status: (byte)CoachClassStatus.Requested);
            classId = cls.ClassId;
        });

        var parentJwt = await LoginAndGetCookie("parent_403");

        // Act — parent (wrong role) tries to staff-respond
        var resp = await _client.SendAsync(
            MakeRequest(HttpMethod.Patch, $"/api/coachclasses/{classId}/staff-respond", parentJwt,
                new { approve = true }));

        // Assert
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            because: "only staff may call staff-respond; a parent must receive 403");
    }
}
