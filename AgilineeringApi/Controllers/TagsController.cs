using AgilineeringApi.Extensions;
using AgilineeringApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AgilineeringApi.Controllers;

[ApiController]
[Route("tags")]
public class TagsController(ITagsService tagsService) : ControllerBase
{
    [HttpGet]
    [EnableRateLimiting("read")]
    public async Task<IActionResult> GetAll() =>
        Ok(await tagsService.GetAllAsync());

    [HttpPost]
    [Authorize(Roles = "admin")]
    [EnableRateLimiting("write")]
    public async Task<IActionResult> Create([FromBody] CreateTagRequest request)
    {
        var result = await tagsService.CreateAsync(request);
        return result.ToActionResult(this, value => StatusCode(201, value));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "admin")]
    [EnableRateLimiting("write")]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await tagsService.DeleteAsync(id);
        return result.ToActionResult(this, NoContent());
    }
}
