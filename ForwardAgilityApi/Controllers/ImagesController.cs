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

    // Magic bytes for each allowed image format
    private static readonly Dictionary<string, byte[][]> MagicBytes = new()
    {
        [".jpg"]  = [[0xFF, 0xD8, 0xFF]],
        [".jpeg"] = [[0xFF, 0xD8, 0xFF]],
        [".png"]  = [[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]],
        [".gif"]  = [[0x47, 0x49, 0x46, 0x38, 0x37, 0x61], [0x47, 0x49, 0x46, 0x38, 0x39, 0x61]],
        [".webp"] = [[0x52, 0x49, 0x46, 0x46]], // RIFF....WEBP
    };

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

        if (!await HasValidMagicBytesAsync(file, ext))
            return BadRequest(new { error = "File contents do not match the declared image type." });

        var imagesPath = configuration["Storage:ImagesPath"] ?? "images";
        var dir = Path.IsPathRooted(imagesPath)
            ? imagesPath
            : Path.Combine(env.ContentRootPath, imagesPath);

        Directory.CreateDirectory(dir);

        var fileName = $"{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(dir, fileName);

        try
        {
            await using var stream = System.IO.File.Create(fullPath);
            await file.CopyToAsync(stream);
        }
        catch
        {
            System.IO.File.Delete(fullPath);
            throw;
        }

        logger.LogInformation("Image uploaded: {FileName}", fileName);

        return Created($"/images/{fileName}", new { url = $"/images/{fileName}" });
    }

    private static async Task<bool> HasValidMagicBytesAsync(IFormFile file, string ext)
    {
        if (!MagicBytes.TryGetValue(ext, out var signatures))
            return false;

        var maxLen = signatures.Max(s => s.Length);
        var header = new byte[maxLen];
        var stream = file.OpenReadStream();
        await using (stream.ConfigureAwait(false))
        {
            var read = await stream.ReadAsync(header.AsMemory(0, maxLen)).ConfigureAwait(false);
            return signatures.Any(sig => header.AsSpan(0, read).StartsWith(sig));
        }
    }
}
