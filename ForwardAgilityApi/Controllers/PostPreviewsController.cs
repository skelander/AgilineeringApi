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
        if (string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { error = "Password is required." });

        var result = await previewService.CreateAsync(postId, request);
        return result.Status switch
        {
            ServiceResultStatus.Ok => Ok(result.Value),
            ServiceResultStatus.NotFound => NotFound(new { error = result.Error }),
            ServiceResultStatus.BadRequest => BadRequest(new { error = result.Error }),
            _ => StatusCode(500)
        };
    }

    [HttpGet]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> GetByPost(int postId)
    {
        var result = await previewService.GetByPostAsync(postId);
        return result.Status switch
        {
            ServiceResultStatus.Ok => Ok(result.Value),
            ServiceResultStatus.NotFound => NotFound(new { error = result.Error }),
            _ => StatusCode(500)
        };
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Delete(int postId, int id)
    {
        var result = await previewService.DeleteAsync(postId, id);
        return result.Status switch
        {
            ServiceResultStatus.Ok => NoContent(),
            ServiceResultStatus.NotFound => NotFound(new { error = result.Error }),
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
            ServiceResultStatus.NotFound => NotFound(new { error = result.Error }),
            ServiceResultStatus.Forbidden => Unauthorized(new { error = "Invalid credentials." }),
            _ => StatusCode(500)
        };
    }
}
