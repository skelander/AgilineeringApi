using AgilineeringApi.Data;
using AgilineeringApi.Models;
using AgilineeringApi.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AgilineeringApi.Services;

public class ImagesService(AppDbContext db, IOptions<ImagesOptions> imagesOptions, ILogger<ImagesService> logger) : IImagesService
{
    private static readonly HashSet<string> AllowedExtensions = [".jpg", ".jpeg", ".png", ".gif", ".webp"];

    // Magic bytes validate that file contents match the declared type, preventing polyglot file attacks
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

    public async Task<IEnumerable<ImageListItem>> ListAsync(CancellationToken ct = default) =>
        await db.Images
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new ImageListItem(i.Filename, i.OriginalFilename, $"/images/{i.Filename}", i.Size, i.CreatedAt))
            .ToListAsync(ct);

    public async Task<(byte[] Data, string ContentType)?> GetAsync(string filename, CancellationToken ct = default)
    {
        var image = await db.Images
            .Where(i => i.Filename == filename)
            .Select(i => new { i.ContentType, i.Data })
            .FirstOrDefaultAsync(ct);
        return image is null ? null : (image.Data, image.ContentType);
    }

    public async Task<ServiceResult<string>> UploadAsync(IFormFile file, CancellationToken ct = default)
    {
        if (file is null || file.Length == 0)
            return ServiceResult<string>.BadRequest("No file provided.");

        var maxFileSize = imagesOptions.Value.MaxFileSizeBytes;
        if (file.Length > maxFileSize)
            return ServiceResult<string>.BadRequest($"File must be {maxFileSize / (1024d * 1024d):F0} MB or smaller.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            return ServiceResult<string>.BadRequest("Only jpg, jpeg, png, gif, and webp images are allowed.");

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);
        var data = ms.ToArray();

        if (!HasValidMagicBytes(data, ext))
            return ServiceResult<string>.BadRequest("File contents do not match the declared image type.");

        var filename = $"{Guid.NewGuid():N}{ext}";
        db.Images.Add(new Image
        {
            Filename = filename,
            OriginalFilename = file.FileName,
            ContentType = ContentTypes[ext],
            Data = data,
            Size = data.Length,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Image {Filename} saved", filename);
        return ServiceResult<string>.Ok(filename);
    }

    public async Task<ServiceResult> DeleteAsync(string filename, CancellationToken ct = default)
    {
        var image = await db.Images.FirstOrDefaultAsync(i => i.Filename == filename, ct);
        if (image is null)
            return ServiceResult.NotFound("Image not found.");

        db.Images.Remove(image);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Image {Filename} removed", filename);
        return ServiceResult.Ok();
    }

    private static bool HasValidMagicBytes(byte[] data, string ext)
    {
        if (!MagicBytes.TryGetValue(ext, out var checks))
            return false;

        bool AllChecksMatch() => checks.All(c =>
            data.Length >= c.Offset + c.Item2.Length &&
            data.AsSpan(c.Offset, c.Item2.Length).SequenceEqual(c.Item2));

        // Some formats (e.g. GIF) have multiple alternative magic byte sequences, all at offset 0.
        // Other formats (e.g. WEBP) require multiple checks at different offsets that must ALL match.
        var hasAlternatives = checks.Length > 1 && checks.All(c => c.Offset == 0);
        if (hasAlternatives)
            return checks.Any(c =>
                data.Length >= c.Item2.Length &&
                data.AsSpan(0, c.Item2.Length).SequenceEqual(c.Item2));

        return AllChecksMatch();
    }
}
