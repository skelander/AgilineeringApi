using AgilineeringApi.Extensions;
using AgilineeringApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AgilineeringApi.Controllers;

[ApiController]
[Route("tags")]
public class TagsController(ITagsService tagsService, ILogger<TagsController> logger) : ControllerBase
{
    [HttpGet]
    [EnableRateLimiting("read")]
    public async Task<IActionResult> GetAll(CancellationToken ct = default) =>
        Ok(await tagsService.GetAllAsync(ct));

    [HttpPost]
    [Authorize(Roles = "admin")]
    [EnableRateLimiting("write")]
    public async Task<IActionResult> Create([FromBody] CreateTagRequest request, CancellationToken ct = default)
    {
        var result = await tagsService.CreateAsync(request, ct);
        if (result.Status == ServiceResultStatus.Ok)
            logger.LogInformation("Admin {User} created tag {Slug}", User.Identity?.Name ?? "unknown", result.Value!.Slug);
        return result.ToActionResult(this, value => Created($"/tags", value));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "admin")]
    [EnableRateLimiting("write")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct = default)
    {
        var result = await tagsService.DeleteAsync(id, ct);
        if (result.Status == ServiceResultStatus.Ok)
            logger.LogInformation("Admin {User} deleted tag {TagId}", User.Identity?.Name ?? "unknown", id);
        return result.ToActionResult(this, NoContent());
    }
}
