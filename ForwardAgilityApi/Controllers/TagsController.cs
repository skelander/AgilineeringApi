using ForwardAgilityApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
    public async Task<IActionResult> Create([FromBody] CreateTagRequest request)
    {
        var result = await tagsService.CreateAsync(request);
        return result.Status switch
        {
            ServiceResultStatus.Ok => CreatedAtAction(nameof(GetAll), result.Value),
            ServiceResultStatus.Conflict => Conflict(new { error = result.Error }),
            _ => StatusCode(500)
        };
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "admin")]
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
}
