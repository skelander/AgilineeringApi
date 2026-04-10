using AgilineeringApi.Extensions;
using AgilineeringApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AgilineeringApi.Controllers;

[ApiController]
[Route("posts")]
public class PostsController(IPostsService postsService, ILogger<PostsController> logger) : ControllerBase
{
    [HttpGet]
    [EnableRateLimiting("read")]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? tag = null,
        [FromQuery] bool includeUnpublished = false)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);
        var showUnpublished = includeUnpublished && User.IsInRole("admin");
        return Ok(await postsService.GetAllAsync(includeUnpublished: showUnpublished, page: page, pageSize: pageSize, tag: tag));
    }

    [HttpGet("{slug}")]
    public async Task<IActionResult> GetBySlug(string slug)
    {
        var result = await postsService.GetBySlugAsync(slug, includeUnpublished: User.IsInRole("admin"));
        return result.ToActionResult(this, Ok);
    }

    [HttpPost]
    [Authorize(Roles = "admin")]
    [EnableRateLimiting("write")]
    public async Task<IActionResult> Create([FromBody] CreatePostRequest request)
    {
        var authorId = User.GetUserId();
        if (authorId is null)
            return Unauthorized(new { error = "Invalid token." });

        var result = await postsService.CreateAsync(request, authorId.Value);
        if (result.Status == ServiceResultStatus.Ok)
            logger.LogInformation("Admin {User} created post {Slug}", User.Identity?.Name ?? "unknown", result.Value!.Slug);
        return result.ToActionResult(this,
            value => CreatedAtAction(nameof(GetBySlug), new { slug = value.Slug }, value));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "admin")]
    [EnableRateLimiting("write")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdatePostRequest request)
    {
        var result = await postsService.UpdateAsync(id, request);
        if (result.Status == ServiceResultStatus.Ok)
            logger.LogInformation("Admin {User} updated post {PostId}", User.Identity?.Name ?? "unknown", id);
        return result.ToActionResult(this, Ok);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "admin")]
    [EnableRateLimiting("write")]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await postsService.DeleteAsync(id);
        if (result.Status == ServiceResultStatus.Ok)
            logger.LogInformation("Admin {User} deleted post {PostId}", User.Identity?.Name ?? "unknown", id);
        return result.ToActionResult(this, NoContent());
    }
}
