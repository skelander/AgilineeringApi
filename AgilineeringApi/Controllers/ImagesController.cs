using AgilineeringApi.Data;
using AgilineeringApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace AgilineeringApi.Controllers;

[ApiController]
[Route("images")]
public class ImagesController(AppDbContext db, ILogger<ImagesController> logger) : ControllerBase
{
    private static readonly HashSet<string> AllowedExtensions = [".jpg", ".jpeg", ".png", ".gif", ".webp"];
    private static readonly long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

    private static readonly Dictionary<string, (int Offset, byte[])[]> MagicBytes = new()
    {
        [".jpg"]  = [(0, [0xFF, 0xD8, 0xFF])],
        [".jpeg"] = [(0, [0xFF, 0xD8, 0xFF])],
        [".png"]  = [(0, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A])],
        [".gif"]  = [(0, [0x47, 0x49, 0x46, 0x38, 0x37, 0x61]), (0, [0x47, 0x49, 0x46, 0x38, 0x39, 0x61])],
        [".webp"] = [(0, [0x52, 0x49, 0x46, 0x46]), (8, [0x57, 0x45, 0x42, 0x50])],
    };

    private static readonly Dictionary<string, string> ContentTypes = new()
    {
        [".jpg"]  = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"]  = "image/png",
        [".gif"]  = "image/gif",
        [".webp"] = "image/webp",
    };

    [HttpGet]
    [Authorize(Roles = "admin")]
    [EnableRateLimiting("read")]
    public async Task<IActionResult> List()
    {
        var images = await db.Images
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new
            {
                filename = i.Filename,
                url = $"/images/{i.Filename}",
                size = i.Size,
                createdAt = i.CreatedAt,
            })
            .ToListAsync();

        return Ok(images);
    }

    [HttpGet("{filename}")]
    [EnableRateLimiting("read")]
    public async Task<IActionResult> Serve(string filename)
    {
        var safeFilename = Path.GetFileName(filename);
        if (safeFilename != filename)
            return BadRequest(new { error = "Invalid filename." });

        var image = await db.Images.FirstOrDefaultAsync(i => i.Filename == safeFilename);
        if (image is null)
            return NotFound();

        Response.Headers["Cache-Control"] = "public, max-age=31536000, immutable";
        return File(image.Data, image.ContentType);
    }

    [HttpDelete("{filename}")]
    [Authorize(Roles = "admin")]
    [EnableRateLimiting("write")]
    public async Task<IActionResult> Delete(string filename)
    {
        var safeFilename = Path.GetFileName(filename);
        if (safeFilename != filename)
            return BadRequest(new { error = "Invalid filename." });

        var image = await db.Images.FirstOrDefaultAsync(i => i.Filename == safeFilename);
        if (image is null)
            return NotFound(new { error = "Image not found." });

        db.Images.Remove(image);
        await db.SaveChangesAsync();
        logger.LogInformation("Admin {User} deleted image {Filename}", User.Identity?.Name ?? "unknown", safeFilename);
        return NoContent();
    }

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

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        var data = ms.ToArray();

        var filename = $"{Guid.NewGuid():N}{ext}";
        var image = new Image
        {
            Filename = filename,
            ContentType = ContentTypes[ext],
            Data = data,
            Size = data.Length,
            CreatedAt = DateTime.UtcNow,
        };

        db.Images.Add(image);
        await db.SaveChangesAsync();

        logger.LogInformation("Admin {User} uploaded image {Filename}", User.Identity?.Name ?? "unknown", filename);
        return Created($"/images/{filename}", new { url = $"/images/{filename}" });
    }

    private static async Task<bool> HasValidMagicBytesAsync(IFormFile file, string ext)
    {
        if (!MagicBytes.TryGetValue(ext, out var checks))
            return false;

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

            bool MatchesAll() => checks.All(c =>
                read >= c.Offset + c.Item2.Length &&
                header.AsSpan(c.Offset, c.Item2.Length).SequenceEqual(c.Item2));

            var hasAlternatives = checks.Length > 1 && checks.All(c => c.Offset == 0);
            if (hasAlternatives)
                return checks.Any(c =>
                    read >= c.Item2.Length &&
                    header.AsSpan(0, c.Item2.Length).SequenceEqual(c.Item2));

            return MatchesAll();
        }
    }
}
