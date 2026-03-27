using ForwardAgilityApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ForwardAgilityApi.Controllers;

[ApiController]
[Route("tags")]
public class TagsController(ITagsService tagsService) : ControllerBase
{

    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(await tagsService.GetAllAsync());

    [HttpPost]
    [Authorize(Roles = "admin")]
    [EnableRateLimiting("write")]
    public async Task<IActionResult> Create([FromBody] CreateTagRequest request)
    {
        var validation = ValidateTagRequest(request);
        if (validation is not null) return validation;

        var result = await tagsService.CreateAsync(request);
        return result.Status switch
        {
            ServiceResultStatus.Ok => Created($"/tags/{result.Value!.Id}", result.Value),
            ServiceResultStatus.Conflict => Conflict(new { error = result.Error }),
            _ => StatusCode(500)
        };
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "admin")]
    [EnableRateLimiting("write")]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await tagsService.DeleteAsync(id);
        return result.Status switch
        {
            ServiceResultStatus.Ok => NoContent(),
            ServiceResultStatus.NotFound => NotFound(new { error = result.Error }),
            _ => StatusCode(500)
        };
    }

    private IActionResult? ValidateTagRequest(CreateTagRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { error = "Name is required." });
        if (request.Name.Length > 100)
            return BadRequest(new { error = "Name must be 100 characters or fewer." });
        if (string.IsNullOrWhiteSpace(request.Slug))
            return BadRequest(new { error = "Slug is required." });
        if (request.Slug.Length > 100)
            return BadRequest(new { error = "Slug must be 100 characters or fewer." });
        if (!SlugValidator.IsValid(request.Slug))
            return BadRequest(new { error = "Slug must contain only lowercase letters, numbers, and hyphens." });
        return null;
    }
}
