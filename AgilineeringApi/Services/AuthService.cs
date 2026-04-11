using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AgilineeringApi.Data;
using AgilineeringApi.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AgilineeringApi.Services;

public class AuthService(
    AppDbContext db,
    IOptions<SecurityOptions> securityOptions,
    IOptions<JwtOptions> jwtOptions,
    ILogger<AuthService> logger) : IAuthService
{
    private readonly SecurityOptions _security = securityOptions.Value;
    private readonly JwtOptions _jwt = jwtOptions.Value;

    public async Task<LoginResult> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Username == request.Username, ct);

        // Return same error for unknown user and wrong password to prevent user enumeration
        if (user is null)
        {
            logger.LogWarning("Failed login attempt for unknown username {Username}", request.Username);
            return new LoginResult(null, "Invalid username or password.", null);
        }

        if (user.LockoutEnd.HasValue && user.LockoutEnd > DateTime.UtcNow)
        {
            logger.LogWarning("Login attempt on locked account {Username}, locked until {LockoutEnd}", user.Username, user.LockoutEnd);
            return new LoginResult(null, "Account is locked.", user.LockoutEnd);
        }

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            user.FailedLoginAttempts++;
            var maxAttempts = Math.Max(1, _security.MaxFailedLoginAttempts);
            if (user.FailedLoginAttempts >= maxAttempts)
            {
                user.LockoutEnd = DateTime.UtcNow.AddMinutes(_security.LockoutDurationMinutes);
                logger.LogWarning("Account {Username} locked out after {Attempts} failed attempts", user.Username, user.FailedLoginAttempts);
            }
            else
            {
                logger.LogWarning("Failed login attempt for {Username} ({Attempts}/{MaxAttempts})", user.Username, user.FailedLoginAttempts, maxAttempts);
            }
            await db.SaveChangesAsync(ct);
            return new LoginResult(null, "Invalid username or password.", null);
        }

        // Successful login — reset lockout state
        user.FailedLoginAttempts = 0;
        user.LockoutEnd = null;
        await db.SaveChangesAsync(ct);
        logger.LogInformation("User {Username} logged in successfully", user.Username);

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, user.Role),
        };
        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(_jwt.ExpiryHours),
            signingCredentials: creds);

        return new LoginResult(new LoginResponse(new JwtSecurityTokenHandler().WriteToken(token), user.Role), null, null);
    }

    public async Task<ServiceResult> ChangePasswordAsync(int userId, ChangePasswordRequest request, CancellationToken ct = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
            return ServiceResult.NotFound("User not found.");

        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
            return ServiceResult.Forbidden("Current password is incorrect.");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword, workFactor: SecurityConstants.PasswordHashWorkFactor);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("User {Username} changed their password", user.Username);
        return ServiceResult.Ok();
    }
}
