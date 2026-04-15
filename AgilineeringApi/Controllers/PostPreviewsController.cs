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
    [HttpGet]
    [Authorize(Roles = "admin")]
    [EnableRateLimiting("read")]
    public async Task<IActionResult> GetAll(int postId, CancellationToken ct = default) =>
        Ok(await previewService.GetAllWithCommentsAsync(postId, ct));

    [HttpPost]
    [Authorize(Roles = "admin")]
    [EnableRateLimiting("write")]
    public async Task<IActionResult> Create(int postId, [FromBody] CreatePreviewRequest request, CancellationToken ct = default)
    {
        var validationError = PreviewPasswordValidator.Validate(request.Password, this);
        if (validationError is not null) return validationError;

        var result = await previewService.CreateAsync(postId, request, ct);
        return result.ToActionResult(this, value => StatusCode(201, value));
    }
}

[ApiController]
[Route("posts/preview")]
public class PostPreviewAccessController(IPostPreviewService previewService) : ControllerBase
{
    [HttpGet("{token}")]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Check(string token, CancellationToken ct = default)
    {
        var exists = await previewService.TokenExistsAsync(token, ct);
        return exists ? NoContent() : NotFound(new { error = "This preview has been removed. Ask the author for a new link." });
    }

    [HttpPost("{token}/access")]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Access(string token, [FromBody] PreviewAccessRequest request, CancellationToken ct = default)
    {
        var validationError = PreviewPasswordValidator.Validate(request.Password, this);
        if (validationError is not null) return validationError;

        var result = await previewService.AccessAsync(token, request, ct);
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
    [EnableRateLimiting("read")]
    public async Task<IActionResult> GetComments(string token, [FromBody] PreviewAccessRequest request, CancellationToken ct = default)
    {
        var validationError = PreviewPasswordValidator.Validate(request.Password, this);
        if (validationError is not null) return validationError;

        var result = await previewService.GetCommentsAsync(token, request, ct);
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

    [HttpPost("{token}/comments")]
    [EnableRateLimiting("write")]
    public async Task<IActionResult> AddComment(string token, [FromBody] CreateCommentRequest request, CancellationToken ct = default)
    {
        var validationError = PreviewPasswordValidator.Validate(request.Password, this);
        if (validationError is not null) return validationError;
        if (string.IsNullOrWhiteSpace(request.Body))
            return BadRequest(new { error = "Comment body is required." });
        if (request.Body.Length > SecurityConstants.MaxCommentBodyLength)
            return BadRequest(new { error = $"Comment must be {SecurityConstants.MaxCommentBodyLength} characters or fewer." });

        var result = await previewService.AddCommentAsync(token, request, ct);
        return result.Status switch
        {
            ServiceResultStatus.Ok => StatusCode(201, result.Value),
            ServiceResultStatus.NotFound or ServiceResultStatus.Forbidden =>
                Unauthorized(new { error = "Invalid token or credentials." }),
            ServiceResultStatus.BadRequest => BadRequest(new { error = result.Error }),
            _ => StatusCode(500)
        };
    }

    [HttpPut("{token}/comments/{commentId:int}")]
    [EnableRateLimiting("write")]
    public async Task<IActionResult> UpdateComment(string token, int commentId, [FromBody] UpdateCommentRequest request, CancellationToken ct = default)
    {
        var validationError = PreviewPasswordValidator.Validate(request.Password, this);
        if (validationError is not null) return validationError;
        if (string.IsNullOrWhiteSpace(request.Body))
            return BadRequest(new { error = "Comment body is required." });
        if (request.Body.Length > SecurityConstants.MaxCommentBodyLength)
            return BadRequest(new { error = $"Comment must be {SecurityConstants.MaxCommentBodyLength} characters or fewer." });

        var result = await previewService.UpdateCommentAsync(token, commentId, request, ct);
        return result.Status switch
        {
            ServiceResultStatus.Ok => Ok(result.Value),
            ServiceResultStatus.NotFound or ServiceResultStatus.Forbidden =>
                Unauthorized(new { error = "Invalid token or credentials." }),
            _ => StatusCode(500)
        };
    }

    [HttpDelete("{token}/comments/{commentId:int}")]
    [EnableRateLimiting("write")]
    public async Task<IActionResult> DeleteComment(string token, int commentId, [FromBody] DeleteCommentRequest request, CancellationToken ct = default)
    {
        var validationError = PreviewPasswordValidator.Validate(request.Password, this);
        if (validationError is not null) return validationError;

        var result = await previewService.DeleteCommentAsync(token, commentId, request, ct);
        return result.Status switch
        {
            ServiceResultStatus.Ok => NoContent(),
            ServiceResultStatus.NotFound or ServiceResultStatus.Forbidden =>
                Unauthorized(new { error = "Invalid token or credentials." }),
            _ => StatusCode(500)
        };
    }

}
