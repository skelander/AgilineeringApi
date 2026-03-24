using ForwardAgilityApi.Data;
using ForwardAgilityApi.Models;
using Microsoft.EntityFrameworkCore;

namespace ForwardAgilityApi.Services;

public class PostsService(AppDbContext db) : IPostsService
{
    public async Task<List<PostSummaryResponse>> GetAllAsync(bool includeUnpublished = false)
    {
        var posts = await db.Posts
            .AsNoTracking()
            .Include(p => p.Author)
            .Include(p => p.Tags)
            .Where(p => includeUnpublished || p.Published)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        return posts
            .Select(p => new PostSummaryResponse(
            p.Id, p.Title, p.Slug, p.Published, p.CreatedAt,
            p.Author.Username,
            p.Tags.Select(t => new TagResponse(t.Id, t.Name, t.Slug)).ToList()))
            .ToList();
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

        var tags = await db.Tags.Where(t => request.TagIds.Contains(t.Id)).ToListAsync();
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
            Tags = tags
        };
        db.Posts.Add(post);
        await db.SaveChangesAsync();
        await db.Entry(post).Reference(p => p.Author).LoadAsync();
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
        post.Tags = await db.Tags.Where(t => request.TagIds.Contains(t.Id)).ToListAsync();

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
