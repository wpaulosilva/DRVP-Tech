using DanceSchoolApp.Server.Data;
using DanceSchoolApp.Server.DTOs;
using DanceSchoolApp.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace DanceSchoolApp.Server.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private const string AccessCookieName  = "access_token";
        private const string RefreshCookieName = "refresh_token";

        private readonly AuthService _authService;
        private readonly IWebHostEnvironment _env;
        private readonly AppDbContext _db;

        public AuthController(AuthService authService, IWebHostEnvironment env, AppDbContext db)
        {
            _authService = authService;
            _env = env;
            _db = db;
        }

        //  POST /api/auth/login
        [HttpPost("login")]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            try
            {
                var (accessToken, rawRefreshToken, response) = await _authService.LoginAsync(request);
                SetAccessCookie(accessToken, response.ExpiresAt);
                SetRefreshCookie(rawRefreshToken);
                return Ok(response);
            }
            catch (ArgumentException ex)          { return BadRequest(ex.Message); }
            catch (UnauthorizedAccessException ex) { return Unauthorized(ex.Message); }
            catch (Exception ex)                   { return StatusCode(500, ex.Message); }
        }

        //  POST /api/auth/refresh
        // Validates the refresh token cookie, checks IsActive, rotates both cookies.
        // Called automatically by the frontend client when a request returns 401.
        [HttpPost("refresh")]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> Refresh()
        {
            var raw = Request.Cookies[RefreshCookieName];
            if (string.IsNullOrWhiteSpace(raw))
                return Unauthorized("No refresh token.");

            try
            {
                var (accessToken, newRaw, response) = await _authService.RefreshAsync(raw);
                SetAccessCookie(accessToken, response.ExpiresAt);
                SetRefreshCookie(newRaw);
                return Ok(response);
            }
            catch (UnauthorizedAccessException ex) { return Unauthorized(ex.Message); }
            catch (Exception ex)                   { return StatusCode(500, ex.Message); }
        }

        //  POST /api/auth/logout
        [HttpPost("logout")]
        [Authorize]
        [EnableRateLimiting("api")]
        public async Task<IActionResult> Logout()
        {
            var raw = Request.Cookies[RefreshCookieName];
            if (!string.IsNullOrWhiteSpace(raw))
                await _authService.RevokeRefreshTokenAsync(raw);

            ClearCookie(AccessCookieName, "/");
            ClearCookie(RefreshCookieName, "/api/auth");
            return NoContent();
        }

        //  POST /api/auth/forgot-password
        [HttpPost("forgot-password")]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            try
            {
                await _authService.SendPasswordResetAsync(request.Email);
                return Ok("If that email is registered, a reset link has been sent.");
            }
            catch (Exception ex) { return StatusCode(500, ex.Message); }
        }

        //  POST /api/auth/reset-password
        [HttpPost("reset-password")]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            try
            {
                await _authService.ResetPasswordAsync(request.Token, request.NewPassword);
                return NoContent();
            }
            catch (UnauthorizedAccessException ex) { return Unauthorized(ex.Message); }
            catch (KeyNotFoundException ex)        { return NotFound(ex.Message); }
            catch (Exception ex)                   { return StatusCode(500, ex.Message); }
        }

        //  GET /api/auth/me
        [HttpGet("me")]
        [Authorize]
        [EnableRateLimiting("api")]
        public async Task<IActionResult> Me()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var username    = User.FindFirst(ClaimTypes.Name)?.Value;
            var roles       = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();

            if (userIdClaim is null || username is null)
                return Unauthorized("Token claims are missing.");

            var userId = int.Parse(userIdClaim);
            var person = await _db.Users
                .Where(u => u.UserId == userId)
                .Select(u => new { u.PersonInfo!.FirstName, u.PersonInfo!.LastName })
                .FirstOrDefaultAsync();

            return Ok(new
            {
                UserId    = userId,
                Username  = username,
                Roles     = roles,
                FirstName = person?.FirstName,
                LastName  = person?.LastName,
            });
        }

        private void SetAccessCookie(string token, DateTime expires)
        {
            Response.Cookies.Append(AccessCookieName, token, new CookieOptions
            {
                HttpOnly = true,
                Secure   = !_env.IsDevelopment(),
                SameSite = SameSiteMode.Strict,
                Expires  = expires,
                Path     = "/"
            });
        }

        private void SetRefreshCookie(string raw)
        {
            Response.Cookies.Append(RefreshCookieName, raw, new CookieOptions
            {
                HttpOnly = true,
                Secure   = !_env.IsDevelopment(),
                SameSite = SameSiteMode.Strict,
                Expires  = DateTime.Now.AddDays(7),
                Path     = "/api/auth"  // only sent to auth endpoints, not every request
            });
        }

        private void ClearCookie(string name, string path)
        {
            Response.Cookies.Append(name, string.Empty, new CookieOptions
            {
                HttpOnly = true,
                Secure   = !_env.IsDevelopment(),
                SameSite = SameSiteMode.Strict,
                Expires  = DateTime.Now.AddDays(-1),
                Path     = path
            });
        }
    }
}
