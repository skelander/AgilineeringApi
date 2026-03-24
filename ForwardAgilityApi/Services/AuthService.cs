using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ForwardAgilityApi.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace ForwardAgilityApi.Services;

public class AuthService(AppDbContext db, IConfiguration configuration) : IAuthService
{
    public async Task<LoginResult> LoginAsync(LoginRequest request)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Username == request.Username);

        // Return same error for unknown user and wrong password to prevent user enumeration
        if (user is null)
            return new LoginResult(null, "Invalid username or password.", null);

        if (user.LockoutEnd.HasValue && user.LockoutEnd > DateTime.UtcNow)
            return new LoginResult(null, "Account is locked.", user.LockoutEnd);

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            user.FailedLoginAttempts++;
            var maxAttempts = configuration.GetValue("Security:MaxFailedLoginAttempts", 5);
            if (user.FailedLoginAttempts >= maxAttempts)
            {
                var lockoutMinutes = configuration.GetValue("Security:LockoutDurationMinutes", 15);
                user.LockoutEnd = DateTime.UtcNow.AddMinutes(lockoutMinutes);
            }
            await db.SaveChangesAsync();
            return new LoginResult(null, "Invalid username or password.", null);
        }

        // Successful login — reset lockout state
        user.FailedLoginAttempts = 0;
        user.LockoutEnd = null;
        await db.SaveChangesAsync();

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
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: creds);

        return new LoginResult(new LoginResponse(new JwtSecurityTokenHandler().WriteToken(token), user.Role), null, null);
    }
}
