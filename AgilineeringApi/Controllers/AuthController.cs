using AgilineeringApi.Data;
using AgilineeringApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace AgilineeringApi.Controllers;

[ApiController]
[Route("auth")]
public class AuthController(IAuthService authService, AppDbContext db) : ControllerBase
{
    [HttpPost("login")]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { error = "Username and password are required." });

        var result = await authService.LoginAsync(request);

        if (result.LockedUntil.HasValue)
            return StatusCode(429, new { error = result.Error, lockedUntil = result.LockedUntil });

        if (result.Response is null)
            return Unauthorized(new { error = result.Error });

        return Ok(result.Response);
    }

    [HttpPost("set-password")]
    public async Task<IActionResult> SetPassword([FromBody] SetPasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.NewPassword))
            return BadRequest(new { error = "Username and newPassword are required." });

        var user = await db.Users.FirstOrDefaultAsync(u => u.Username == request.Username);
        if (user is null)
            return NotFound(new { error = "User not found." });

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword, workFactor: 12);
        user.FailedLoginAttempts = 0;
        user.LockoutEnd = null;
        await db.SaveChangesAsync();

        return Ok(new { message = "Password updated." });
    }
}

public record SetPasswordRequest(string Username, string NewPassword);
