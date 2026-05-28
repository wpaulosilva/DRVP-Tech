using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using DanceSchoolApp.Server.DTOs.Classes;
using DanceSchoolApp.Tests.Helpers;

namespace DanceSchoolApp.Tests.Integration;

[Trait("Category", "Integration")]
public class CoachClassLifecycleTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public CoachClassLifecycleTests(CustomWebApplicationFactory factory)
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
            because: $"login as '{username}' must succeed before the lifecycle steps run");

        var setCookie = response.Headers
            .GetValues("Set-Cookie")
            .FirstOrDefault(h => h.StartsWith("access_token="));

        setCookie.Should().NotBeNull(because: "login response must set the access_token cookie");
        return setCookie!.Split(';')[0].Substring("access_token=".Length);
    }

    /// <summary>
    /// Creates a request with the jwt cookie pre-attached. Content is optional;
    /// pass an anonymous object and it is serialised to JSON automatically.
    /// </summary>
    private static HttpRequestMessage MakeRequest(
        HttpMethod method, string url, string jwt, object? body = null)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Add("Cookie", $"access_token={jwt}");

        if (body is not null)
        {
            req.Content = new StringContent(
                JsonSerializer.Serialize(body),
                Encoding.UTF8,
                "application/json");
        }

        return req;
    }

    //  test 

    [Fact]
    public async Task FullClassLifecycle_RequestedToValidated_AllTransitionsSucceed()
    {
        //  Seed 
        // Capture the generated IDs so they can be used in the HTTP calls below.
        // The SeedDatabase lambda is synchronous — no await inside it.
        int coachId = 0, modalityId = 0, studentId = 0;

        _factory.SeedDatabase(db =>
        {
            SeedData.SeedAppSettings(db);

            var modality = SeedData.SeedModality(db, "Ballet Clássico");
            modalityId = modality.ModalityId;

            // Studio must be linked to the modality so CreateAsync can auto-select it.
            var studio = SeedData.SeedStudio(db, "Estúdio A");
            studio.IdModalities.Add(modality);
            db.SaveChanges();

            SeedData.SeedUserWithRole(db, "staff_lc", "staff");

            var coachUser = SeedData.SeedUserWithRole(db, "coach_lc", "coach");
            var coach = SeedData.SeedCoach(db, coachUser);
            coachId = coach.CoachId; // CoachId == coachUser.UserId
            // Ensure the coach teaches the modality used in this scenario
            coach.IdModalities.Add(modality);
            db.SaveChanges();

            // Coach available every Monday 09:00–12:00 (covers the 10–11h test window)
            SeedData.SeedCoachAvailability(db, coach,
                weekday:   (byte)DayOfWeek.Monday,
                startTime: new TimeOnly(9, 0),
                endTime:   new TimeOnly(12, 0));

            var parentUser = SeedData.SeedUserWithRole(db, "parent_lc", "parent");
            var student = SeedData.SeedStudent(db, parentUser, "Ana"); // AcceptanceStatus = 1
            studentId = student.StudentId;
            // Student must be enrolled in the modality for class creation to succeed
            student.IdModalities.Add(modality);
            db.SaveChanges();
        });

        // Compute a class window that is always in the future (next Monday 10-11h UTC).
        // daysUntilMonday == 0 only when today IS Monday, in which case we take
        // the following Monday so the window is at least 7 days out.
        var utcNow = DateTime.UtcNow;
        var daysUntilMonday = ((int)DayOfWeek.Monday - (int)utcNow.DayOfWeek + 7) % 7;
        if (daysUntilMonday == 0) daysUntilMonday = 7;

        // DateTime.UtcNow.Date loses the UTC Kind; restore it so "O" appends "Z".
        var startDt = DateTime.SpecifyKind(
            utcNow.Date.AddDays(daysUntilMonday).AddHours(10),
            DateTimeKind.Utc);
        var endDt = startDt.AddHours(1);

        //  Log in once per role — cookies are kept for the whole test 
        var parentJwt = await LoginAndGetCookie("parent_lc");

        //  Step 1 — Parent creates the class request 
        var createResp = await _client.SendAsync(
            MakeRequest(HttpMethod.Post, "/api/coachclasses", parentJwt, new
            {
                coachId,
                modalityId,
                startDatetime = startDt.ToString("O"),
                endDatetime   = endDt.ToString("O"),
                studentId
            }));

        createResp.StatusCode.Should().Be(HttpStatusCode.Created,
            because: "a valid class request from a parent must return 201 Created");

        var createBody = await createResp.Content.ReadAsStringAsync();
        using var createDoc = JsonDocument.Parse(createBody);
        int classId = createDoc.RootElement.GetProperty("classId").GetInt32();
        classId.Should().BeGreaterThan(0, because: "response body must contain a positive classId");


        //  Step 2 — Coach accepts (Requested → CoachApproved)
        var coachJwt = await LoginAndGetCookie("coach_lc");
        var acceptResp = await _client.SendAsync(
            MakeRequest(HttpMethod.Patch, $"/api/coachclasses/{classId}/coach-respond", coachJwt,
                new { accept = true }));

        acceptResp.StatusCode.Should().Be(HttpStatusCode.NoContent,
            because: "coach-respond (accept=true) on a Requested class must return 204");

        //  Step 3 — Staff approves (CoachApproved → Approved)
        var staffJwt = await LoginAndGetCookie("staff_lc");
        var approveResp = await _client.SendAsync(
            MakeRequest(HttpMethod.Patch, $"/api/coachclasses/{classId}/staff-respond", staffJwt,
                new { approve = true }));

        approveResp.StatusCode.Should().Be(HttpStatusCode.NoContent,
            because: "staff-respond (approve=true) on a CoachApproved class must return 204");

        //  Step 4 — Simulate worker transitioning Approved → Finished 
        // The /finish endpoint has been removed; the transition is now automated
        // by ClassLifecycleWorker. Directly update DB state to replicate what
        // the worker does so the rest of the lifecycle can proceed.
        _factory.SeedDatabase(db =>
        {
            var cls = db.CoachClasses.Find(classId)!;
            cls.Status = (byte)CoachClassStatus.Finished;
            cls.FinishedAt = DateTime.UtcNow.AddHours(-1);
        });

        //  Step 5 — Coach validates (records DidTeach; class stays Finished  
        //            until the single participant also responds in step 6)
        coachJwt = await LoginAndGetCookie("coach_lc");
        var coachValidateResp = await _client.SendAsync(
            MakeRequest(HttpMethod.Patch, $"/api/coachclasses/{classId}/coach-validate", coachJwt,
                new { didTeach = true }));

        coachValidateResp.StatusCode.Should().Be(HttpStatusCode.NoContent,
            because: "coach-validate on a Finished class must return 204");

        //  Step 6a — Staff retrieves the participant list to get the ID 
        staffJwt = await LoginAndGetCookie("staff_lc");
        var participantsResp = await _client.SendAsync(
            MakeRequest(HttpMethod.Get, $"/api/participants/class/{classId}", staffJwt));

        participantsResp.StatusCode.Should().Be(HttpStatusCode.OK,
            because: "staff can always query participants for a class");

        var participantsBody = await participantsResp.Content.ReadAsStringAsync();
        using var participantsDoc = JsonDocument.Parse(participantsBody);
        int participantId = participantsDoc.RootElement
            .GetProperty("items")[0]
            .GetProperty("participantId")
            .GetInt32();
        participantId.Should().BeGreaterThan(0);

        //  Step 6b — Parent validates participant attendance 
        // This is the LAST required response (coach already validated in step 5),
        // so ParticipantService.TryAdvanceClassToStaffReviewAsync auto-advances
        // the class from Finished → Pending.
        parentJwt = await LoginAndGetCookie("parent_lc");
        var parentValidateResp = await _client.SendAsync(
            MakeRequest(HttpMethod.Patch, $"/api/participants/{participantId}/parent-validate", parentJwt,
                new { attended = true }));

        parentValidateResp.StatusCode.Should().Be(HttpStatusCode.NoContent,
            because: "parent-validate on a Finished-class participant must return 204");

        //  Step 7 — Staff final sign-off (Pending → Validated) 
        staffJwt = await LoginAndGetCookie("staff_lc");
        var staffValidateResp = await _client.SendAsync(
            MakeRequest(HttpMethod.Patch, $"/api/coachclasses/{classId}/staff-validate", staffJwt,
                new { confirmed = true, reason = (string?)null }));

        staffValidateResp.StatusCode.Should().Be(HttpStatusCode.NoContent,
            because: "staff-validate on a Pending class must return 204");

        //  Step 8 — Verify final status == Validated (5) 
        var getClassResp = await _client.SendAsync(
            MakeRequest(HttpMethod.Get, $"/api/coachclasses/{classId}", staffJwt));

        getClassResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var classBody = await getClassResp.Content.ReadAsStringAsync();
        using var classDoc = JsonDocument.Parse(classBody);

        classDoc.RootElement
            .GetProperty("status")
            .GetInt32()
            .Should().Be((int)CoachClassStatus.Validated,
                because: "completing every lifecycle step must leave the class in Validated (5) status");
    }
}
