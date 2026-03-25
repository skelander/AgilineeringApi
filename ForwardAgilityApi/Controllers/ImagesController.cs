using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ForwardAgilityApi.Controllers;

[ApiController]
[Route("images")]
public class ImagesController(IConfiguration configuration, IWebHostEnvironment env, ILogger<ImagesController> logger) : ControllerBase
{
    private static readonly HashSet<string> AllowedExtensions = [".jpg", ".jpeg", ".png", ".gif", ".webp"];
    private static readonly long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

    [HttpPost]
    [Authorize(Roles = "admin")]
    [EnableRateLimiting("write")]
    public async Task<IActionResult> Upload(IFormFile file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "No file provided." });

        if (file.Length > MaxFileSizeBytes)
            return BadRequest(new { error = "File must be 10 MB or smaller." });

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            return BadRequest(new { error = "Only jpg, jpeg, png, gif, and webp images are allowed." });

        var imagesPath = configuration["Storage:ImagesPath"] ?? "images";
        var dir = Path.IsPathRooted(imagesPath)
            ? imagesPath
            : Path.Combine(env.ContentRootPath, imagesPath);

        Directory.CreateDirectory(dir);

        var fileName = $"{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(dir, fileName);

        await using var stream = System.IO.File.Create(fullPath);
        await file.CopyToAsync(stream);

        logger.LogInformation("Image uploaded: {FileName}", fileName);

        return Created($"/images/{fileName}", new { url = $"/images/{fileName}" });
    }
}
