using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AgilineeringApi.Controllers;

[ApiController]
[Route("images")]
public class ImagesController(IConfiguration configuration, IWebHostEnvironment env, ILogger<ImagesController> logger) : ControllerBase
{
    private static readonly HashSet<string> AllowedExtensions = [".jpg", ".jpeg", ".png", ".gif", ".webp"];
    private static readonly long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

    // Magic bytes for each allowed image format: (offset, signature)
    private static readonly Dictionary<string, (int Offset, byte[])[]> MagicBytes = new()
    {
        [".jpg"]  = [(0, [0xFF, 0xD8, 0xFF])],
        [".jpeg"] = [(0, [0xFF, 0xD8, 0xFF])],
        [".png"]  = [(0, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A])],
        [".gif"]  = [(0, [0x47, 0x49, 0x46, 0x38, 0x37, 0x61]), (0, [0x47, 0x49, 0x46, 0x38, 0x39, 0x61])],
        // WEBP: bytes 0-3 = "RIFF", bytes 8-11 = "WEBP"
        [".webp"] = [(0, [0x52, 0x49, 0x46, 0x46]), (8, [0x57, 0x45, 0x42, 0x50])],
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
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save uploaded image {FileName}", fileName);
            System.IO.File.Delete(fullPath);
            throw;
        }

        logger.LogInformation("Image uploaded: {FileName}", fileName);

        return Created($"/images/{fileName}", new { url = $"/images/{fileName}" });
    }

    private static async Task<bool> HasValidMagicBytesAsync(IFormFile file, string ext)
    {
        if (!MagicBytes.TryGetValue(ext, out var checks))
            return false;

        // For formats with multiple alternatives (e.g. GIF87a/GIF89a), each entry is one alternative.
        // For formats requiring multiple markers at different offsets (e.g. WEBP = RIFF + WEBP),
        // all checks for that extension must pass.
        var bufSize = checks.Max(c => c.Offset + c.Item2.Length);
        var header = new byte[bufSize];
        var stream = file.OpenReadStream();
        await using (stream.ConfigureAwait(false))
        {
            var read = 0;
            while (read < bufSize)
            {
                var n = await stream.ReadAsync(header.AsMemory(read, bufSize - read)).ConfigureAwait(false);
                if (n == 0) break;
                read += n;
            }

            // Group checks by whether the format has alternatives (offset 0 only)
            // vs. required multi-marker validation (WEBP: all checks must pass)
            bool MatchesAll() => checks.All(c =>
                read >= c.Offset + c.Item2.Length &&
                header.AsSpan(c.Offset, c.Item2.Length).SequenceEqual(c.Item2));

            // For single-offset formats with alternatives, any match suffices
            var hasAlternatives = checks.Length > 1 && checks.All(c => c.Offset == 0);
            if (hasAlternatives)
                return checks.Any(c =>
                    read >= c.Item2.Length &&
                    header.AsSpan(0, c.Item2.Length).SequenceEqual(c.Item2));

            return MatchesAll();
        }
    }
}
