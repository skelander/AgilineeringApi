using AgilineeringApi.Extensions;
using AgilineeringApi.Options;
using AgilineeringApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace AgilineeringApi.Controllers;

[ApiController]
[Route("auth")]
public class AuthController(
    IAuthService authService,
    IOptions<JwtOptions> jwtOptions,
    IWebHostEnvironment env) : ControllerBase
{
    private readonly JwtOptions _jwt = jwtOptions.Value;

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

        SetAuthCookie(result.Response.Token);
        return Ok(new { role = result.Response.Role });
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        DeleteAuthCookie();
        return NoContent();
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

        var userId = User.GetUserId();
        if (userId is null)
            return Unauthorized(new { error = "Invalid token." });

        var result = await authService.ChangePasswordAsync(userId.Value, request);
        return result.ToActionResult(this, NoContent());
    }

    private void SetAuthCookie(string token) =>
        Response.Cookies.Append("auth_token", token, new CookieOptions
        {
            HttpOnly = true,
            Secure = env.IsProduction(),
            SameSite = env.IsProduction() ? SameSiteMode.None : SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddHours(_jwt.ExpiryHours),
            Path = "/"
        });

    private void DeleteAuthCookie() =>
        Response.Cookies.Delete("auth_token", new CookieOptions
        {
            HttpOnly = true,
            Secure = env.IsProduction(),
            SameSite = env.IsProduction() ? SameSiteMode.None : SameSiteMode.Lax,
            Path = "/"
        });
}
