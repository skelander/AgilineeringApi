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
        var validationError = ValidatePassword(request.Password);
        if (validationError is not null) return validationError;

        var result = await previewService.CreateAsync(postId, request, ct);
        return result.ToActionResult(this, value => StatusCode(201, value));
    }

    private IActionResult? ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
            return BadRequest(new { error = "Password is required." });
        if (password.Length < 6)
            return BadRequest(new { error = "Password must be at least 6 characters." });
        if (password.Length > SecurityConstants.MaxPasswordLength)
            return BadRequest(new { error = $"Password must be {SecurityConstants.MaxPasswordLength} characters or fewer." });
        return null;
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
        var validationError = ValidatePassword(request.Password);
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
        var validationError = ValidatePassword(request.Password);
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
        var validationError = ValidatePassword(request.Password);
        if (validationError is not null) return validationError;
        if (string.IsNullOrWhiteSpace(request.Body))
            return BadRequest(new { error = "Comment body is required." });
        if (request.Body.Length > 5000)
            return BadRequest(new { error = "Comment must be 5000 characters or fewer." });

        var result = await previewService.AddCommentAsync(token, request, ct);
        return result.Status switch
        {
            ServiceResultStatus.Ok => StatusCode(201, result.Value),
            // Return the same 401 for both missing token and wrong credentials
            // to prevent token enumeration via status code differences
            ServiceResultStatus.NotFound or ServiceResultStatus.Forbidden =>
                Unauthorized(new { error = "Invalid token or credentials." }),
            ServiceResultStatus.BadRequest => BadRequest(new { error = result.Error }),
            _ => StatusCode(500)
        };
    }

    private IActionResult? ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
            return BadRequest(new { error = "Password is required." });
        if (password.Length < 6)
            return BadRequest(new { error = "Password must be at least 6 characters." });
        if (password.Length > SecurityConstants.MaxPasswordLength)
            return BadRequest(new { error = $"Password must be {SecurityConstants.MaxPasswordLength} characters or fewer." });
        return null;
    }
}
