using System.Security.Claims;
using ForwardAgilityApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ForwardAgilityApi.Controllers;

[ApiController]
[Route("posts")]
public class PostsController(IPostsService postsService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var isAdmin = User.IsInRole("admin");
        return Ok(await postsService.GetAllAsync(includeUnpublished: isAdmin));
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
    public async Task<IActionResult> Create([FromBody] CreatePostRequest request)
    {
        var authorId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await postsService.CreateAsync(request, authorId);
        return result.Status switch
        {
            ServiceResultStatus.Ok => CreatedAtAction(nameof(GetBySlug), new { slug = result.Value!.Slug }, result.Value),
            ServiceResultStatus.Conflict => Conflict(new { error = result.Error }),
            _ => StatusCode(500)
        };
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdatePostRequest request)
    {
        var result = await postsService.UpdateAsync(id, request);
        return result.Status switch
        {
            ServiceResultStatus.Ok => Ok(result.Value),
            ServiceResultStatus.NotFound => NotFound(new { error = result.Error }),
            ServiceResultStatus.Conflict => Conflict(new { error = result.Error }),
            _ => StatusCode(500)
        };
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "admin")]
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
}
