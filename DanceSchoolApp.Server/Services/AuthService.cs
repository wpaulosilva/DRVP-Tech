using DanceSchoolApp.Server.Data;
using DanceSchoolApp.Server.DTOs;
using DanceSchoolApp.Server.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace DanceSchoolApp.Server.Services
{
    public class AuthService
    {
        private const int AccessTokenMinutes = 5;
        private const int RefreshTokenDays   = 7;

        private readonly AppDbContext _context;
        private readonly EmailService _emailService;
        private readonly IConfiguration _config;

        public AuthService(AppDbContext context, EmailService emailService, IConfiguration config)
        {
            _context = context;
            _emailService = emailService;
            _config = config;
        }

        public async Task<(string AccessToken, string RawRefreshToken, LoginResponse Response)> LoginAsync(
            LoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Username) &&
                string.IsNullOrWhiteSpace(request.Email))
                throw new ArgumentException("Either username or email is required.");

            var user = await _context.Users
                .Include(u => u.IdRoles)
                .FirstOrDefaultAsync(u =>
                    (!string.IsNullOrWhiteSpace(request.Username) && u.Username == request.Username) ||
                    (!string.IsNullOrWhiteSpace(request.Email)    && u.Email    == request.Email));

            if (user is null)
                throw new UnauthorizedAccessException("Invalid credentials.");

            if (!user.IsActive)
                throw new UnauthorizedAccessException("This account is inactive.");

            if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
                throw new UnauthorizedAccessException("Invalid credentials.");

            var roles = user.IdRoles.Select(r => r.RoleName).ToList();
            return await IssueTokenPairAsync(user.UserId, user.Username, roles);
        }

        // Validates the refresh token, checks IsActive, rotates the token.
        public async Task<(string AccessToken, string RawRefreshToken, LoginResponse Response)> RefreshAsync(
            string rawRefreshToken)
        {
            var tokenHash = HashToken(rawRefreshToken);

            var stored = await _context.RefreshTokens
                .Include(t => t.IdUserNavigation)
                    .ThenInclude(u => u.IdRoles)
                .FirstOrDefaultAsync(t => t.TokenHash == tokenHash);

            if (stored is null || stored.RevokedAt is not null || stored.ExpiresAt <= DateTime.Now)
                throw new UnauthorizedAccessException("Invalid or expired refresh token.");

            if (!stored.IdUserNavigation.IsActive)
                throw new UnauthorizedAccessException("This account is inactive.");

            // Revoke the consumed token before issuing a new pair (rotation)
            stored.RevokedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            var roles = stored.IdUserNavigation.IdRoles.Select(r => r.RoleName).ToList();
            return await IssueTokenPairAsync(
                stored.IdUserNavigation.UserId,
                stored.IdUserNavigation.Username,
                roles);
        }

        public async Task RevokeRefreshTokenAsync(string rawRefreshToken)
        {
            var tokenHash = HashToken(rawRefreshToken);
            var stored    = await _context.RefreshTokens
                .FirstOrDefaultAsync(t => t.TokenHash == tokenHash && t.RevokedAt == null);

            if (stored is null) return; // already revoked or not found — idempotent

            stored.RevokedAt = DateTime.Now;
            await _context.SaveChangesAsync();
        }

        public async Task SendPasswordResetAsync(string email)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user is null) return;

            var token       = GeneratePasswordResetToken(user.UserId, user.Email!);
            var frontendBase = _config["App:FrontendBaseUrl"]
                ?? throw new InvalidOperationException("App:FrontendBaseUrl is not configured.");
            var resetLink = $"{frontendBase}/reset-password?token={token}";
            await _emailService.SendPasswordResetEmailAsync(user.Email!, resetLink);
        }

        public async Task ResetPasswordAsync(string token, string newPassword)
        {
            var principal = ValidatePasswordResetToken(token);

            if (principal is null)
                throw new UnauthorizedAccessException("Invalid or expired reset token.");

            var userIdClaim  = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var purposeClaim = principal.FindFirst("purpose")?.Value;

            if (userIdClaim is null || purposeClaim != "pwd_reset")
                throw new UnauthorizedAccessException("Invalid reset token.");

            int userId = int.Parse(userIdClaim);
            var user   = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);

            if (user is null)
                throw new KeyNotFoundException("User not found.");

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            await _context.SaveChangesAsync();
        }

        public string GeneratePasswordResetToken(int userId, string email)
        {
            var secret      = Environment.GetEnvironmentVariable("DanceSchoolApp_JWT_Secret")!;
            var key         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, userId.ToString()),
                new("purpose", "pwd_reset"),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var token = new JwtSecurityToken(
                issuer:            "DanceSchoolApp",
                audience:          "DanceSchoolApp",
                claims:            claims,
                expires:           DateTime.Now.AddHours(24),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        // Issues a new access + refresh token pair and persists the refresh token.
        // Prunes old expired/revoked tokens for the user to keep the table lean.
        private async Task<(string AccessToken, string RawRefreshToken, LoginResponse Response)> IssueTokenPairAsync(
            int userId, string username, List<string> roles)
        {
            var accessExpiry = DateTime.Now.AddMinutes(AccessTokenMinutes);
            var accessToken  = GenerateAccessToken(userId, username, roles, accessExpiry);

            var rawRefreshToken = GenerateRawRefreshToken();

            await _context.RefreshTokens
                .Where(t => t.IdUser == userId &&
                            (t.RevokedAt != null || t.ExpiresAt <= DateTime.Now))
                .ExecuteDeleteAsync();

            _context.RefreshTokens.Add(new RefreshToken
            {
                IdUser    = userId,
                TokenHash = HashToken(rawRefreshToken),
                ExpiresAt = DateTime.Now.AddDays(RefreshTokenDays),
                CreatedAt = DateTime.Now
            });

            await _context.SaveChangesAsync();

            return (accessToken, rawRefreshToken, new LoginResponse
            {
                UserId    = userId,
                Username  = username,
                Roles     = roles,
                ExpiresAt = accessExpiry
            });
        }

        private ClaimsPrincipal? ValidatePasswordResetToken(string token)
        {
            var secret = Environment.GetEnvironmentVariable("DanceSchoolApp_JWT_Secret")!;
            var key    = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));

            var validationParams = new TokenValidationParameters
            {
                ValidateIssuer           = true,
                ValidateAudience         = true,
                ValidateLifetime         = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer              = "DanceSchoolApp",
                ValidAudience            = "DanceSchoolApp",
                IssuerSigningKey         = key
            };

            try { return new JwtSecurityTokenHandler().ValidateToken(token, validationParams, out _); }
            catch { return null; }
        }

        private static string GenerateAccessToken(
            int userId, string username, List<string> roles, DateTime expiresAt)
        {
            var secret = Environment.GetEnvironmentVariable("DanceSchoolApp_JWT_Secret");

            if (string.IsNullOrWhiteSpace(secret))
                throw new InvalidOperationException(
                    "JWT secret is not configured. Set the 'DanceSchoolApp_JWT_Secret' environment variable.");

            if (secret.Length < 32)
                throw new InvalidOperationException("JWT secret must be at least 32 characters long.");

            var key         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub,        userId.ToString()),
                new(JwtRegisteredClaimNames.UniqueName, username),
                new(JwtRegisteredClaimNames.Jti,        Guid.NewGuid().ToString())
            };

            foreach (var role in roles)
                claims.Add(new Claim(ClaimTypes.Role, role));

            var token = new JwtSecurityToken(
                issuer:            "DanceSchoolApp",
                audience:          "DanceSchoolApp",
                claims:            claims,
                expires:           expiresAt,
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        // 48 random bytes → 96-char hex string (URL-safe, no padding needed)
        private static string GenerateRawRefreshToken()
            => Convert.ToHexString(RandomNumberGenerator.GetBytes(48));

        // SHA-256 of the raw token → 64-char hex stored in DB
        private static string HashToken(string raw)
            => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
    }
}
