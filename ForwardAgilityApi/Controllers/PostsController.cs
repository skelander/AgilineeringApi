using System.Security.Claims;
using ForwardAgilityApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ForwardAgilityApi.Controllers;

[ApiController]
[Route("posts")]
public class PostsController(IPostsService postsService) : ControllerBase
{

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? tag = null)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);
        var isAdmin = User.IsInRole("admin");
        return Ok(await postsService.GetAllAsync(includeUnpublished: isAdmin, page: page, pageSize: pageSize, tag: tag));
    }

    [HttpGet("{slug}")]
    public async Task<IActionResult> GetBySlug(string slug)
    {
        var isAdmin = User.IsInRole("admin");
        var result = await postsService.GetBySlugAsync(slug, includeUnpublished: isAdmin);
        return result.Status switch
        {
            ServiceResultStatus.Ok => Ok(result.Value),
            ServiceResultStatus.NotFound => NotFound(new { error = result.Error }),
            _ => StatusCode(500)
        };
    }

    [HttpPost]
    [Authorize(Roles = "admin")]
    [EnableRateLimiting("write")]
    public async Task<IActionResult> Create([FromBody] CreatePostRequest request)
    {
        var validation = ValidatePostFields(request.Title, request.Content, request.Slug);
        if (validation is not null) return validation;

        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var authorId))
            return Unauthorized(new { error = "Invalid token." });

        var result = await postsService.CreateAsync(request, authorId);
        return result.Status switch
        {
            ServiceResultStatus.Ok => CreatedAtAction(nameof(GetBySlug), new { slug = result.Value!.Slug }, result.Value),
            ServiceResultStatus.Conflict => Conflict(new { error = result.Error }),
            ServiceResultStatus.BadRequest => BadRequest(new { error = result.Error }),
            _ => StatusCode(500)
        };
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "admin")]
    [EnableRateLimiting("write")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdatePostRequest request)
    {
        var validation = ValidatePostFields(request.Title, request.Content, request.Slug);
        if (validation is not null) return validation;

        var result = await postsService.UpdateAsync(id, request);
        return result.Status switch
        {
            ServiceResultStatus.Ok => Ok(result.Value),
            ServiceResultStatus.NotFound => NotFound(new { error = result.Error }),
            ServiceResultStatus.Conflict => Conflict(new { error = result.Error }),
            ServiceResultStatus.BadRequest => BadRequest(new { error = result.Error }),
            _ => StatusCode(500)
        };
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "admin")]
    [EnableRateLimiting("write")]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await postsService.DeleteAsync(id);
        return result.Status switch
        {
            ServiceResultStatus.Ok => NoContent(),
            ServiceResultStatus.NotFound => NotFound(new { error = result.Error }),
            _ => StatusCode(500)
        };
    }

    private IActionResult? ValidatePostFields(string title, string content, string slug)
    {
        if (string.IsNullOrWhiteSpace(title))
            return BadRequest(new { error = "Title is required." });
        if (title.Length > 300)
            return BadRequest(new { error = "Title must be 300 characters or fewer." });
        if (string.IsNullOrWhiteSpace(content))
            return BadRequest(new { error = "Content is required." });
        if (string.IsNullOrWhiteSpace(slug))
            return BadRequest(new { error = "Slug is required." });
        if (slug.Length > 300)
            return BadRequest(new { error = "Slug must be 300 characters or fewer." });
        if (!SlugValidator.IsValid(slug))
            return BadRequest(new { error = "Slug must contain only lowercase letters, numbers, and hyphens." });
        return null;
    }
}
