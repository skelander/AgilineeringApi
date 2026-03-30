using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AgilineeringApi.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace AgilineeringApi.Services;

public class AuthService(AppDbContext db, IConfiguration configuration, ILogger<AuthService> logger) : IAuthService
{
    public async Task<LoginResult> LoginAsync(LoginRequest request)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Username == request.Username);

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
            var maxAttempts = Math.Max(1, configuration.GetValue("Security:MaxFailedLoginAttempts", 5));
            if (user.FailedLoginAttempts >= maxAttempts)
            {
                var lockoutMinutes = configuration.GetValue("Security:LockoutDurationMinutes", 15);
                user.LockoutEnd = DateTime.UtcNow.AddMinutes(lockoutMinutes);
                logger.LogWarning("Account {Username} locked out after {Attempts} failed attempts", user.Username, user.FailedLoginAttempts);
            }
            else
            {
                logger.LogWarning("Failed login attempt for {Username} ({Attempts}/{MaxAttempts})", user.Username, user.FailedLoginAttempts, maxAttempts);
            }
            await db.SaveChangesAsync();
            return new LoginResult(null, "Invalid username or password.", null);
        }

        // Successful login — reset lockout state
        user.FailedLoginAttempts = 0;
        user.LockoutEnd = null;
        await db.SaveChangesAsync();
        logger.LogInformation("User {Username} logged in successfully", user.Username);

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, user.Role),
        };
        var token = new JwtSecurityToken(
            issuer: configuration["Jwt:Issuer"],
            audience: configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(configuration.GetValue("Jwt:ExpiryHours", 8)),
            signingCredentials: creds);

        return new LoginResult(new LoginResponse(new JwtSecurityTokenHandler().WriteToken(token), user.Role), null, null);
    }
}
