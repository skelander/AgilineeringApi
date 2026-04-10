using AgilineeringApi.Extensions;
using AgilineeringApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AgilineeringApi.Controllers;

[ApiController]
[Route("posts/{postId:int}/previews")]
public class PostPreviewsController(IPostPreviewService previewService) : ControllerBase
{
    [HttpPost]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Create(int postId, [FromBody] CreatePreviewRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { error = "Password is required." });
        if (request.Password.Length < 6)
            return BadRequest(new { error = "Password must be at least 6 characters." });

        var result = await previewService.CreateAsync(postId, request);
        return result.ToActionResult(this, value => StatusCode(201, value));
    }
}

[ApiController]
[Route("posts/preview")]
public class PostPreviewAccessController(IPostPreviewService previewService) : ControllerBase
{
    [HttpGet("{token}")]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Check(string token)
    {
        var exists = await previewService.TokenExistsAsync(token);
        return exists ? Ok() : NotFound(new { error = "This preview has been removed. Ask the author for a new link." });
    }

    [HttpPost("{token}/access")]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Access(string token, [FromBody] PreviewAccessRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { error = "Password is required." });

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

    [HttpPost("{token}/comments/list")]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> GetComments(string token, [FromBody] PreviewAccessRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { error = "Password is required." });

        var result = await previewService.GetCommentsAsync(token, request);
        return result.Status switch
        {
            ServiceResultStatus.Ok => Ok(result.Value),
            ServiceResultStatus.NotFound => NotFound(new { error = "This preview has been removed. Ask the author for a new link." }),
            ServiceResultStatus.Forbidden => Unauthorized(new { error = "Invalid credentials." }),
            _ => StatusCode(500)
        };
    }

    [HttpPost("{token}/comments")]
    [EnableRateLimiting("write")]
    public async Task<IActionResult> AddComment(string token, [FromBody] CreateCommentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { error = "Password is required." });
        if (string.IsNullOrWhiteSpace(request.Body))
            return BadRequest(new { error = "Comment body is required." });
        if (request.Body.Length > 5000)
            return BadRequest(new { error = "Comment must be 5000 characters or fewer." });

        var result = await previewService.AddCommentAsync(token, request);
        return result.Status switch
        {
            ServiceResultStatus.Ok => StatusCode(201, result.Value),
            ServiceResultStatus.NotFound => NotFound(new { error = "This preview has been removed. Ask the author for a new link." }),
            ServiceResultStatus.Forbidden => Unauthorized(new { error = "Invalid credentials." }),
            _ => StatusCode(500)
        };
    }
}
