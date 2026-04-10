using AgilineeringApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AgilineeringApi.Controllers;

[ApiController]
[Route("images")]
public class ImagesController(IImagesService imagesService, ILogger<ImagesController> logger) : ControllerBase
{
    private const string ImmutableCacheHeader = "public, max-age=31536000, immutable";
    [HttpGet]
    [Authorize(Roles = "admin")]
    [EnableRateLimiting("read")]
    public async Task<IActionResult> List() =>
        Ok(await imagesService.ListAsync());

    [HttpGet("{filename}")]
    [EnableRateLimiting("read")]
    public async Task<IActionResult> Serve(string filename)
    {
        var safeFilename = Path.GetFileName(filename);
        if (safeFilename != filename)
            return BadRequest(new { error = "Invalid filename." });

        var result = await imagesService.GetAsync(safeFilename);
        if (result is null)
            return NotFound();

        Response.Headers["Cache-Control"] = ImmutableCacheHeader;
        return File(result.Value.Data, result.Value.ContentType);
    }

    [HttpDelete("{filename}")]
    [Authorize(Roles = "admin")]
    [EnableRateLimiting("write")]
    public async Task<IActionResult> Delete(string filename)
    {
        var safeFilename = Path.GetFileName(filename);
        if (safeFilename != filename)
            return BadRequest(new { error = "Invalid filename." });

        var result = await imagesService.DeleteAsync(safeFilename);
        if (result.Status == ServiceResultStatus.NotFound)
            return NotFound(new { error = result.Error });

        logger.LogInformation("Admin {User} deleted image {Filename}", User.Identity?.Name ?? "unknown", safeFilename);
        return NoContent();
    }

    [HttpPost]
    [Authorize(Roles = "admin")]
    [EnableRateLimiting("write")]
    public async Task<IActionResult> Upload(IFormFile file)
    {
        var result = await imagesService.UploadAsync(file);
        if (result.Status != ServiceResultStatus.Ok)
            return BadRequest(new { error = result.Error });

        var filename = result.Value!;
        logger.LogInformation("Admin {User} uploaded image {Filename}", User.Identity?.Name ?? "unknown", filename);
        return Created($"/images/{filename}", new { url = $"/images/{filename}" });
    }
}
