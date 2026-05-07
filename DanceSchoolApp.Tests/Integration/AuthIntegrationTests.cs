using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using DanceSchoolApp.Tests.Helpers;

namespace DanceSchoolApp.Tests.Integration;

[Trait("Category", "Integration")]
public class AuthIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AuthIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        // AllowAutoRedirect=false so we see exact status codes (no silent 307→200).
        // BaseAddress=https://localhost so UseHttpsRedirection does not issue a 307
        // before we even reach the controller.
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
            BaseAddress = new Uri("https://localhost")
        });
    }

    //  helpers 

    /// <summary>
    /// Logs in and returns the raw JWT value from the Set-Cookie header.
    /// Fails the test immediately if login does not return 200.
    /// </summary>
    private async Task<string> LoginAndGetJwtAsync(string username, string password = "Test1234!")
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new { username, password });

        response.StatusCode.Should().Be(
            HttpStatusCode.OK,
            because: $"login as '{username}' must succeed before the actual test assertion");

        return ExtractJwtFromResponse(response);
    }

    private static string ExtractJwtFromResponse(HttpResponseMessage response)
    {
        var setCookie = response.Headers
            .GetValues("Set-Cookie")
            .FirstOrDefault(h => h.StartsWith("access_token="));

        setCookie.Should().NotBeNull(because: "login response must include the access_token Set-Cookie header");

        return setCookie!.Split(';')[0].Substring("access_token=".Length);
    }

    //  tests 

    [Fact]
    public async Task Login_ValidCredentials_Returns200AndSetsCookie()
    {
        // Arrange
        _factory.SeedDatabase(db =>
        {
            SeedData.SeedUserWithRole(db, "staff_login_ok", "staff");
        });
        var body = new { username = "staff_login_ok", password = "Test1234!" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", body);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.TryGetValues("Set-Cookie", out var cookies).Should().BeTrue();
        cookies!.Should().Contain(c => c.StartsWith("access_token="),
            because: "a successful login must set the access_token HttpOnly cookie");
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        // Arrange
        _factory.SeedDatabase(db =>
        {
            SeedData.SeedUserWithRole(db, "staff_wrong_pwd", "staff");
        });
        var body = new { username = "staff_wrong_pwd", password = "WrongPass!" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", body);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_InactiveUser_Returns401()
    {
        // Arrange
        _factory.SeedDatabase(db =>
        {
            var user = SeedData.SeedUserWithRole(db, "staff_inactive", "staff");
            user.IsActive = false;
            db.SaveChanges();
        });
        var body = new { username = "staff_inactive", password = "Test1234!" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", body);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AuthMe_WithValidCookie_Returns200AndUserData()
    {
        // Arrange — seed user and log in to obtain the jwt cookie value
        _factory.SeedDatabase(db =>
        {
            SeedData.SeedUserWithRole(db, "staff_me_test", "staff");
        });
        var jwtValue = await LoginAndGetJwtAsync("staff_me_test");

        // Act — manually attach cookie; the HttpClient jar may not forward HttpOnly
        // cookies reliably in the TestServer environment across request boundaries
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Add("Cookie", $"jwt={jwtValue}");
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("username").GetString()
            .Should().Be("staff_me_test");
    }

    [Fact]
    public async Task AuthMe_WithoutCookie_Returns401()
    {
        // Act — fresh client with no cookie
        var response = await _client.GetAsync("/api/auth/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_WithValidCookie_ClearsJwtCookie()
    {
        // Arrange
        _factory.SeedDatabase(db =>
        {
            SeedData.SeedUserWithRole(db, "staff_logout", "staff");
        });
        var jwtValue = await LoginAndGetJwtAsync("staff_logout");

        // Act
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        request.Headers.Add("Cookie", $"jwt={jwtValue}");
        var response = await _client.SendAsync(request);

        // Assert — 204 No Content and the cookie is overwritten with an empty,
        // already-expired value (the controller sets Expires = UtcNow - 1 day)
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var logoutCookie = response.Headers
            .GetValues("Set-Cookie")
            .FirstOrDefault(h => h.StartsWith("access_token="));

        logoutCookie.Should().NotBeNull(because: "logout must emit a Set-Cookie header to clear the access_token");
        logoutCookie!.Split(';')[0].Should().Be("access_token=",
            because: "the cleared cookie must have an empty value");
    }
}
