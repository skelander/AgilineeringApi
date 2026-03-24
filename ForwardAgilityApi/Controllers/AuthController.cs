using ForwardAgilityApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace ForwardAgilityApi.Controllers;

[ApiController]
[Route("auth")]
public class AuthController(IAuthService authService) : ControllerBase
{
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await authService.LoginAsync(request);
        if (result is null)
            return Unauthorized(new { error = "Invalid username or password." });
        return Ok(result);
    }
}
