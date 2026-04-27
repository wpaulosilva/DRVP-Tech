using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using DanceSchoolApp.Server.Data;
using DanceSchoolApp.Server.DTOs.Classes;
using DanceSchoolApp.Server.Models;
using DanceSchoolApp.Tests.Helpers;

namespace DanceSchoolApp.Tests.Integration;

[Trait("Category", "Integration")]
public class BillingIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public BillingIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
            BaseAddress = new Uri("https://localhost")
        });
    }

    //  helpers 

    private async Task<string> LoginAndGetCookie(string username, string password = "Test1234!")
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new { username, password });

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            because: $"login as '{username}' must succeed before billing assertions");

        var setCookie = response.Headers
            .GetValues("Set-Cookie")
            .FirstOrDefault(h => h.StartsWith("jwt="));

        setCookie.Should().NotBeNull(because: "login must issue the jwt cookie");
        return setCookie!.Split(';')[0].Substring("jwt=".Length);
    }

    /// <summary>
    /// Returns the first Monday of the given year/month at midnight UTC.
    /// When the 1st of the month is itself a Monday, <c>daysToMonday == 0</c>
    /// and that day is returned directly.
    /// </summary>
    private static DateTime FirstMondayOfMonth(int year, int month)
    {
        var firstOfMonth = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        int daysToMonday = ((int)DayOfWeek.Monday - (int)firstOfMonth.DayOfWeek + 7) % 7;
        return firstOfMonth.AddDays(daysToMonday);
    }

    /// <summary>
    /// Seeds all entities required by a billing test scenario.
    /// <paramref name="suffix"/> keeps usernames and resource names unique
    /// across tests that share the same SQLite DB via the class fixture.
    /// <paramref name="classStart"/> sets the class window; the end is +2 h.
    /// </summary>
    private static void SeedBillingScenario(
        AppDbContext db,
        string suffix,
        byte classStatus,
        DateTime classStart)
    {
        SeedData.SeedAppSettings(db);

        var modality = SeedData.SeedModality(db, $"Modal {suffix}");
        var studio   = SeedData.SeedStudio(db,   $"Studio {suffix}");

        var staff     = SeedData.SeedUserWithRole(db, $"staff_{suffix}",  "staff");
        var coachUser = SeedData.SeedUserWithRole(db, $"coach_{suffix}",  "coach");
        var coach     = SeedData.SeedCoach(db, coachUser);
        var parent    = SeedData.SeedUserWithRole(db, $"parent_{suffix}", "parent");
        var student   = SeedData.SeedStudent(db, parent, "Ana");

        // Insert the class directly at the requested status.
        // Studio ↔ Modality join is not needed here because we bypass CreateAsync.
        var coachClass = SeedData.SeedCoachClass(
            db, coach, modality, studio, createdByUser: staff,
            start: classStart,
            durationMinutes: 120,       // 2h — determines the billing amount
            status: classStatus);

        // Attach the student as a participant.  ValidationStatus is set to
        // ParentConfirmed (1) because BillingService only checks class status,
        // not participant validation status, but this reflects a realistic state
        // for a Validated class.
        db.Participants.Add(new Participant
        {
            IdCoachClass     = coachClass.ClassId,
            IdStudent        = student.StudentId,
            JoinedAt         = DateOnly.FromDateTime(DateTime.UtcNow),
            ValidationStatus = (byte)ParticipantValidationStatus.ParentConfirmed
        });
        db.SaveChanges();
    }

    //  tests 

    [Fact]
    public async Task GetStudentBilling_ValidatedClasses_ReturnsCorrectAmounts()
    {
        // Arrange — Validated class on a Monday (weekday) in the current month.
        // Expected amount: 2h × 36.00 (weekday rate) = 72.00
        var now        = DateTime.UtcNow;
        var classStart = FirstMondayOfMonth(now.Year, now.Month).AddHours(10);
        var queryMonth = now.ToString("yyyy-MM");

        _factory.SeedDatabase(db =>
        {
            SeedBillingScenario(db, "bill_v", (byte)CoachClassStatus.Validated, classStart);
        });

        var staffJwt = await LoginAndGetCookie("staff_bill_v");

        // Act
        var req = new HttpRequestMessage(HttpMethod.Get,
            $"/api/staff/billing/students?month={queryMonth}&page=1&pageSize=25");
        req.Headers.Add("Cookie", $"jwt={staffJwt}");
        var response = await _client.SendAsync(req);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        // At least one billing row must appear for the seeded student
        root.GetProperty("items").GetArrayLength()
            .Should().BeGreaterThan(0,
                because: "a Validated class in the queried month must produce at least one billing row");

        // First row (items are sorted by StudentName — only one student here)
        root.GetProperty("items")[0].GetProperty("totalAmount").GetDecimal()
            .Should().Be(72.00m,
                because: "2 hours × weekday rate 36.00 = 72.00");

        // Summary covers the whole month
        root.GetProperty("summary").GetProperty("totalRevenue").GetDecimal()
            .Should().BeGreaterThanOrEqualTo(72.00m,
                because: "summary.totalRevenue must account for the seeded student's 72.00");
    }

    [Fact]
    public async Task GetStudentBilling_NoValidatedClasses_ReturnsEmptyItems()
    {
        // Arrange — Finished (4) class in the PREVIOUS month.
        // Using a different month from the Validated-class test above ensures
        // the two tests do not contaminate each other: both share the same
        // SQLite DB via IClassFixture, so a different query month gives each
        // test a clean slice of data.
        var prevMonth  = DateTime.UtcNow.AddMonths(-1);
        var classStart = FirstMondayOfMonth(prevMonth.Year, prevMonth.Month).AddHours(10);
        var queryMonth = prevMonth.ToString("yyyy-MM");

        _factory.SeedDatabase(db =>
        {
            SeedBillingScenario(db, "bill_f", (byte)CoachClassStatus.Finished, classStart);
        });

        var staffJwt = await LoginAndGetCookie("staff_bill_f");

        // Act
        var req = new HttpRequestMessage(HttpMethod.Get,
            $"/api/staff/billing/students?month={queryMonth}&page=1&pageSize=25");
        req.Headers.Add("Cookie", $"jwt={staffJwt}");
        var response = await _client.SendAsync(req);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            because: "billing endpoint always returns 200, even when no data matches");

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("items").GetArrayLength()
            .Should().Be(0,
                because: "BillingService only counts Validated (5) classes; " +
                         "a Finished (4) class must not appear in items");
    }
}
