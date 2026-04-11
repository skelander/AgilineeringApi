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
    public async Task<IActionResult> List(CancellationToken ct = default) =>
        Ok(await imagesService.ListAsync(ct));

    [HttpGet("{filename}")]
    [EnableRateLimiting("read")]
    public async Task<IActionResult> Serve(string filename, CancellationToken ct = default)
    {
        var safeFilename = Path.GetFileName(filename);
        if (safeFilename != filename)
            return BadRequest(new { error = "Invalid filename." });

        var result = await imagesService.GetAsync(safeFilename, ct);
        if (result is null)
            return NotFound(new { error = "Image not found." });

        Response.Headers["Cache-Control"] = ImmutableCacheHeader;
        return File(result.Value.Data, result.Value.ContentType);
    }

    [HttpDelete("{filename}")]
    [Authorize(Roles = "admin")]
    [EnableRateLimiting("write")]
    public async Task<IActionResult> Delete(string filename, CancellationToken ct = default)
    {
        var safeFilename = Path.GetFileName(filename);
        if (safeFilename != filename)
            return BadRequest(new { error = "Invalid filename." });

        var result = await imagesService.DeleteAsync(safeFilename, ct);
        if (result.Status == ServiceResultStatus.NotFound)
            return NotFound(new { error = result.Error });

        logger.LogInformation("Admin {User} deleted image {Filename}", User.Identity?.Name ?? "unknown", safeFilename);
        return NoContent();
    }

    [HttpPost]
    [Authorize(Roles = "admin")]
    [EnableRateLimiting("write")]
    public async Task<IActionResult> Upload(IFormFile file, CancellationToken ct = default)
    {
        var result = await imagesService.UploadAsync(file, ct);
        if (result.Status != ServiceResultStatus.Ok)
            return BadRequest(new { error = result.Error });

        var filename = result.Value!;
        logger.LogInformation("Admin {User} uploaded image {Filename}", User.Identity?.Name ?? "unknown", filename);
        return Created($"/images/{filename}", new UploadedImageResponse($"/images/{filename}"));
    }
}
