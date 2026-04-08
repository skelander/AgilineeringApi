using System.Security.Claims;
using AgilineeringApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AgilineeringApi.Controllers;

[ApiController]
[Route("auth")]
public class AuthController(IAuthService authService) : ControllerBase
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

    [HttpPost("change-password")]
    [Authorize(Roles = "admin")]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CurrentPassword) || string.IsNullOrWhiteSpace(request.NewPassword))
            return BadRequest(new { error = "Current password and new password are required." });
        if (request.NewPassword.Length < 12)
            return BadRequest(new { error = "New password must be at least 12 characters." });

        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return Unauthorized(new { error = "Invalid token." });

        var result = await authService.ChangePasswordAsync(userId, request);
        return result.Status switch
        {
            ServiceResultStatus.Ok => NoContent(),
            ServiceResultStatus.Forbidden => StatusCode(403, new { error = result.Error }),
            ServiceResultStatus.NotFound => NotFound(new { error = result.Error }),
            _ => StatusCode(500)
        };
    }
}
