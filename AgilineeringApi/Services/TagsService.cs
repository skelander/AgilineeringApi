using AgilineeringApi.Data;
using AgilineeringApi.Utilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AgilineeringApi.Services;

public class TagsService(AppDbContext db) : ITagsService
{
    private const int MaxNameLength = 100;
    private const int MaxSlugLength = 100;

    public async Task<List<TagResponse>> GetAllAsync(CancellationToken ct = default) =>
        await db.Tags
            .OrderBy(t => t.Name)
            .Select(t => new TagResponse(t.Id, t.Name, t.Slug))
            .ToListAsync(ct);

    public async Task<ServiceResult<TagResponse>> CreateAsync(CreateTagRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return ServiceResult<TagResponse>.BadRequest("Name is required.");
        if (request.Name.Length > MaxNameLength)
            return ServiceResult<TagResponse>.BadRequest($"Name must be {MaxNameLength} characters or fewer.");
        if (string.IsNullOrWhiteSpace(request.Slug))
            return ServiceResult<TagResponse>.BadRequest("Slug is required.");
        if (request.Slug.Length > MaxSlugLength)
            return ServiceResult<TagResponse>.BadRequest($"Slug must be {MaxSlugLength} characters or fewer.");
        if (!SlugValidator.IsValid(request.Slug))
            return ServiceResult<TagResponse>.BadRequest("Slug must contain only lowercase letters, numbers, and hyphens.");

        if (await db.Tags.AnyAsync(t => t.Slug == request.Slug, ct))
            return ServiceResult<TagResponse>.Conflict($"Tag with slug '{request.Slug}' already exists.");

        if (await db.Tags.AnyAsync(t => t.Name.ToLower() == request.Name.ToLower(), ct))
            return ServiceResult<TagResponse>.Conflict($"Tag with name '{request.Name}' already exists.");

        var tag = new Models.Tag { Name = request.Name, Slug = request.Slug };
        db.Tags.Add(tag);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqliteException { SqliteErrorCode: SqliteErrorCodes.UniqueConstraintViolation })
        {
            return ServiceResult<TagResponse>.Conflict($"Tag with slug '{request.Slug}' or name '{request.Name}' already exists.");
        }
        return ServiceResult<TagResponse>.Ok(new TagResponse(tag.Id, tag.Name, tag.Slug));
    }

    public async Task<ServiceResult> DeleteAsync(int id, CancellationToken ct = default)
    {
        var tag = await db.Tags.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (tag is null)
            return ServiceResult.NotFound($"Tag {id} not found.");

        db.Tags.Remove(tag);
        await db.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }
}
