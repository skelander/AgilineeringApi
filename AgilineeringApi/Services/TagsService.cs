using AgilineeringApi.Data;
using Microsoft.EntityFrameworkCore;

namespace AgilineeringApi.Services;

public class TagsService(AppDbContext db) : ITagsService
{
    public async Task<List<TagResponse>> GetAllAsync()
    {
        return await db.Tags
            .OrderBy(t => t.Name)
            .Select(t => new TagResponse(t.Id, t.Name, t.Slug))
            .ToListAsync();
    }

    public async Task<ServiceResult<TagResponse>> CreateAsync(CreateTagRequest request)
    {
        if (await db.Tags.AnyAsync(t => t.Slug == request.Slug))
            return ServiceResult<TagResponse>.Conflict($"Tag with slug '{request.Slug}' already exists.");

        if (await db.Tags.AnyAsync(t => t.Name == request.Name))
            return ServiceResult<TagResponse>.Conflict($"Tag with name '{request.Name}' already exists.");

        var tag = new Models.Tag { Name = request.Name, Slug = request.Slug };
        db.Tags.Add(tag);
        await db.SaveChangesAsync();
        return ServiceResult<TagResponse>.Ok(new TagResponse(tag.Id, tag.Name, tag.Slug));
    }

    public async Task<ServiceResult> DeleteAsync(int id)
    {
        var tag = await db.Tags.FindAsync(id);
        if (tag is null)
            return ServiceResult.NotFound($"Tag {id} not found.");

        db.Tags.Remove(tag);
        await db.SaveChangesAsync();
        return ServiceResult.Ok();
    }
}
