using ForwardAgilityApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ForwardAgilityApi.Controllers;

[ApiController]
[Route("posts/{postId:int}/previews")]
public class PostPreviewsController(IPostPreviewService previewService) : ControllerBase
{
    [HttpPost]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Create(int postId, [FromBody] CreatePreviewRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { error = "Name is required." });
        if (request.Name.Length > 200)
            return BadRequest(new { error = "Name must be 200 characters or fewer." });
        if (string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { error = "Password is required." });
        if (request.Password.Length < 6)
            return BadRequest(new { error = "Password must be at least 6 characters." });

        var result = await previewService.CreateAsync(postId, request);
        return result.Status switch
        {
            ServiceResultStatus.Ok => Ok(result.Value),
            ServiceResultStatus.NotFound => NotFound(new { error = result.Error }),
            ServiceResultStatus.BadRequest => BadRequest(new { error = result.Error }),
            _ => StatusCode(500)
        };
    }
}

[ApiController]
[Route("posts/preview")]
public class PostPreviewAccessController(IPostPreviewService previewService) : ControllerBase
{
    [HttpPost("{token}/access")]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Access(string token, [FromBody] PreviewAccessRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { error = "Name and password are required." });

        var result = await previewService.AccessAsync(token, request);
        return result.Status switch
        {
            ServiceResultStatus.Ok => Ok(result.Value),
            // Return the same 401 for both missing token and wrong credentials
            // to prevent token enumeration via status code differences
            ServiceResultStatus.NotFound or ServiceResultStatus.Forbidden =>
                Unauthorized(new { error = "Invalid token or credentials." }),
            _ => StatusCode(500)
        };
    }
}
