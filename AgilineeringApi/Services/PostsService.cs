using AgilineeringApi.Data;
using AgilineeringApi.Models;
using Microsoft.EntityFrameworkCore;

namespace AgilineeringApi.Services;

public class PostsService(AppDbContext db) : IPostsService
{
    public async Task<PagedResult<PostSummaryResponse>> GetAllAsync(bool includeUnpublished = false, int page = 1, int pageSize = 10, string? tag = null)
    {
        var query = db.Posts
            .AsNoTracking()
            .Include(p => p.Author)
            .Include(p => p.Tags)
            .Where(p => includeUnpublished || p.Published);

        if (tag is not null)
            query = query.Where(p => p.Tags.Any(t => t.Slug == tag));

        var totalCount = await query.CountAsync();

        var posts = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var items = posts
            .Select(p => new PostSummaryResponse(
                p.Id, p.Title, p.Slug, p.Published, p.CreatedAt,
                p.Author.Username,
                p.Tags.Select(t => new TagResponse(t.Id, t.Name, t.Slug)).ToList()))
            .ToList();

        return new PagedResult<PostSummaryResponse>(items, page, pageSize, totalCount);
    }

    public async Task<ServiceResult<PostDetailResponse>> GetBySlugAsync(string slug, bool includeUnpublished = false)
    {
        var post = await db.Posts
            .AsNoTracking()
            .Include(p => p.Author)
            .Include(p => p.Tags)
            .FirstOrDefaultAsync(p => p.Slug == slug && (includeUnpublished || p.Published));

        if (post is null)
            return ServiceResult<PostDetailResponse>.NotFound($"Post '{slug}' not found.");

        return ServiceResult<PostDetailResponse>.Ok(ToDetail(post));
    }

    public async Task<ServiceResult<PostDetailResponse>> CreateAsync(CreatePostRequest request, int authorId)
    {
        if (await db.Posts.AnyAsync(p => p.Slug == request.Slug))
            return ServiceResult<PostDetailResponse>.Conflict($"Post with slug '{request.Slug}' already exists.");

        var requestedTagIds = request.TagIds.ToList();
        var tags = await db.Tags.Where(t => requestedTagIds.Contains(t.Id)).ToListAsync();
        var missingIds = requestedTagIds.Except(tags.Select(t => t.Id)).ToList();
        if (missingIds.Count > 0)
            return ServiceResult<PostDetailResponse>.BadRequest($"Tag IDs not found: {string.Join(", ", missingIds)}.");

        var author = await db.Users.FindAsync(authorId)
            ?? throw new InvalidOperationException($"Author {authorId} not found.");
        var now = DateTime.UtcNow;
        var post = new Post
        {
            Title = request.Title,
            Content = request.Content,
            Slug = request.Slug,
            Published = request.Published,
            CreatedAt = now,
            UpdatedAt = now,
            AuthorId = authorId,
            Author = author,
            Tags = tags
        };
        db.Posts.Add(post);
        await db.SaveChangesAsync();
        return ServiceResult<PostDetailResponse>.Ok(ToDetail(post));
    }

    public async Task<ServiceResult<PostDetailResponse>> UpdateAsync(int id, UpdatePostRequest request)
    {
        var post = await db.Posts
            .Include(p => p.Author)
            .Include(p => p.Tags)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (post is null)
            return ServiceResult<PostDetailResponse>.NotFound($"Post {id} not found.");

        if (post.Slug != request.Slug && await db.Posts.AnyAsync(p => p.Slug == request.Slug))
            return ServiceResult<PostDetailResponse>.Conflict($"Post with slug '{request.Slug}' already exists.");

        post.Title = request.Title;
        post.Content = request.Content;
        post.Slug = request.Slug;
        post.Published = request.Published;
        post.UpdatedAt = DateTime.UtcNow;
        var updatedTagIds = request.TagIds.ToList();
        var updatedTags = await db.Tags.Where(t => updatedTagIds.Contains(t.Id)).ToListAsync();
        var missingTagIds = updatedTagIds.Except(updatedTags.Select(t => t.Id)).ToList();
        if (missingTagIds.Count > 0)
            return ServiceResult<PostDetailResponse>.BadRequest($"Tag IDs not found: {string.Join(", ", missingTagIds)}.");

        post.Tags = updatedTags;

        await db.SaveChangesAsync();
        return ServiceResult<PostDetailResponse>.Ok(ToDetail(post));
    }

    public async Task<ServiceResult> DeleteAsync(int id)
    {
        var post = await db.Posts.FindAsync(id);
        if (post is null)
            return ServiceResult.NotFound($"Post {id} not found.");

        db.Posts.Remove(post);
        await db.SaveChangesAsync();
        return ServiceResult.Ok();
    }

    private static PostDetailResponse ToDetail(Post post) => new(
        post.Id, post.Title, post.Content, post.Slug, post.Published,
        post.CreatedAt, post.UpdatedAt,
        post.Author.Username,
        post.Tags.Select(t => new TagResponse(t.Id, t.Name, t.Slug)));
}
