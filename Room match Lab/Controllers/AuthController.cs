using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Google.Apis.Auth;
using Microsoft.IdentityModel.Tokens;
using RoomMates.Data;
using RoomMates.Models;
using RoomMates.Services;

namespace RoomMates.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class AuthController(
    RoomMatesDbContext dbContext,
    IConfiguration configuration,
    IGoogleTokenValidator googleTokenValidator,
    IPasswordHasher<Profile> passwordHasher) : ControllerBase
{
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponseDto>> Register(
        RegisterRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var email = request.Email.Trim().ToLowerInvariant();
        if (await dbContext.Profiles.AnyAsync(
                profile => profile.Email.ToLower() == email,
                cancellationToken))
            return Conflict(new { error = "An account with this email already exists. Sign in with its existing provider." });

        var profile = new Profile
        {
            Id = Guid.NewGuid(),
            FullName = request.FullName.Trim(),
            Email = email,
            Role = "Student",
            IsApproved = true,
            AvatarUrl = "https://www.gravatar.com/avatar/?d=mp",
            TenantId = DetectTenant(email)
        };
        profile.PasswordHash = passwordHasher.HashPassword(profile, request.Password);

        dbContext.Profiles.Add(profile);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(await CreateAuthenticatedResponseAsync(profile));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponseDto>> Login(
        PasswordLoginRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var email = request.Email.Trim();
        var profile = await dbContext.Profiles
            .FirstOrDefaultAsync(
                existing => existing.Email.ToLower() == email.ToLower(),
                cancellationToken);
        if (profile?.PasswordHash is null)
            return Unauthorized(new { error = "Invalid email or password." });

        var verification = passwordHasher.VerifyHashedPassword(
            profile,
            profile.PasswordHash,
            request.Password);
        if (verification == PasswordVerificationResult.Failed)
            return Unauthorized(new { error = "Invalid email or password." });

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            profile.PasswordHash = passwordHasher.HashPassword(profile, request.Password);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return Ok(await CreateAuthenticatedResponseAsync(profile));
    }

    [HttpPost("google")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponseDto>> Google(
        GoogleLoginRequestDto request,
        CancellationToken cancellationToken)
    {
        var idToken = string.IsNullOrWhiteSpace(request.IdToken) ? request.Credential : request.IdToken;
        if (string.IsNullOrWhiteSpace(idToken))
            return BadRequest(new { error = "IdToken or Credential is required." });

        GoogleJsonWebSignature.Payload googlePayload;
        try
        {
            googlePayload = await googleTokenValidator.ValidateAsync(idToken);
        }
        catch (InvalidJwtException)
        {
            return Unauthorized();
        }

        var email = googlePayload.Email?.Trim();
        if (!googlePayload.EmailVerified || string.IsNullOrWhiteSpace(email))
            return Unauthorized();

        var profile = await dbContext.Profiles
            .FirstOrDefaultAsync(
                existing => existing.Email.ToLower() == email.ToLower(),
                cancellationToken);

        if (profile is null)
        {
            profile = new Profile
            {
                Id = Guid.NewGuid(),
                Email = email,
                Role = "Student",
                IsApproved = true,
                FullName = string.IsNullOrWhiteSpace(googlePayload.Name) ? email : googlePayload.Name.Trim(),
                AvatarUrl = string.IsNullOrWhiteSpace(googlePayload.Picture)
                    ? "https://www.gravatar.com/avatar/?d=mp"
                    : googlePayload.Picture,
                TenantId = DetectTenant(email)
            };

            dbContext.Profiles.Add(profile);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return Ok(await CreateAuthenticatedResponseAsync(profile));
    }

    [HttpPut("password")]
    [Authorize]
    public async Task<IActionResult> SetPassword(
        SetPasswordRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var profileId))
            return Unauthorized();

        var profile = await dbContext.Profiles.FindAsync([profileId], cancellationToken);
        if (profile is null)
            return Unauthorized();

        if (profile.PasswordHash is not null)
        {
            var verification = passwordHasher.VerifyHashedPassword(
                profile,
                profile.PasswordHash,
                request.CurrentPassword ?? string.Empty);
            if (verification == PasswordVerificationResult.Failed)
                return Unauthorized(new { error = "Current password is incorrect." });
        }

        profile.PasswordHash = passwordHasher.HashPassword(profile, request.Password);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("refresh")]
    [Authorize]
    public async Task<ActionResult<LoginResponseDto>> Refresh(CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var profileId))
            return Unauthorized();
        var profile = await dbContext.Profiles.FirstOrDefaultAsync(item => item.Id == profileId, cancellationToken);
        if (profile is null) return Unauthorized();
        return Ok(await CreateAuthenticatedResponseAsync(profile));
    }

    [HttpPost("logout")]
    [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme)]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }

    private async Task<LoginResponseDto> CreateAuthenticatedResponseAsync(Profile profile)
    {
        var issuer = configuration["Jwt:Issuer"]
            ?? throw new InvalidOperationException("Jwt:Issuer is not configured.");
        var audience = configuration["Jwt:Audience"]
            ?? throw new InvalidOperationException("Jwt:Audience is not configured.");
        var key = configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("Jwt:Key is not configured.");

        var loginResponse = CreateLoginResponse(profile, issuer, audience, key);
        var cookieIdentity = new ClaimsIdentity(
            CreateProfileClaims(profile),
            CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(cookieIdentity),
            new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = loginResponse.ExpiresAtUtc
            });

        return loginResponse;
    }

    private static string? DetectTenant(string email)
    {
        if (email.EndsWith("@knu.ua", StringComparison.OrdinalIgnoreCase)) return "KNU";
        if (email.EndsWith("@kpi.ua", StringComparison.OrdinalIgnoreCase)) return "KPI";
        return null;
    }

    private static LoginResponseDto CreateLoginResponse(
        Profile profile,
        string issuer,
        string audience,
        string key)
    {
        var keyBytes = Encoding.UTF8.GetBytes(key);
        if (keyBytes.Length < 32)
            throw new InvalidOperationException("Jwt:Key must contain at least 32 UTF-8 bytes for HS256.");

        var expiresAtUtc = DateTime.UtcNow.AddHours(1);
        var signingKey = new SymmetricSecurityKey(keyBytes);
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        var payload = new JwtPayload(
            issuer,
            audience,
            CreateProfileClaims(profile),
            notBefore: null,
            expires: expiresAtUtc);
        var token = new JwtSecurityToken(new JwtHeader(credentials), payload);

        return new LoginResponseDto(new JwtSecurityTokenHandler().WriteToken(token), expiresAtUtc);
    }

    private static IEnumerable<Claim> CreateProfileClaims(Profile profile)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, profile.Id.ToString()),
            new(ClaimTypes.Email, profile.Email),
            new(ClaimTypes.Role, profile.Role),
            new("IsApproved", profile.IsApproved.ToString()),
            new("EnableDetailedAnalytics", profile.EnableDetailedAnalytics.ToString()),
            new("IsCrossUniversity", profile.IsCrossUniversity.ToString())
        };

        if (!string.IsNullOrWhiteSpace(profile.TenantId))
            claims.Add(new Claim("TenantId", profile.TenantId));

        return claims;
    }
}

public sealed class GoogleLoginRequestDto
{
    public string? IdToken { get; init; }
    public string? Credential { get; init; }
}

public sealed class RegisterRequestDto
{
    [Required, StringLength(200)] public string FullName { get; init; } = string.Empty;
    [Required, EmailAddress, StringLength(320)] public string Email { get; init; } = string.Empty;
    [Required, StringLength(128, MinimumLength = 8)] public string Password { get; init; } = string.Empty;
}

public sealed class PasswordLoginRequestDto
{
    [Required, EmailAddress, StringLength(320)] public string Email { get; init; } = string.Empty;
    [Required, StringLength(128, MinimumLength = 8)] public string Password { get; init; } = string.Empty;
}

public sealed class SetPasswordRequestDto
{
    [StringLength(128, MinimumLength = 8)] public string? CurrentPassword { get; init; }
    [Required, StringLength(128, MinimumLength = 8)] public string Password { get; init; } = string.Empty;
}

public sealed record LoginResponseDto(string Token, DateTime ExpiresAtUtc);
