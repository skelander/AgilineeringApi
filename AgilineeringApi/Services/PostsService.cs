using AgilineeringApi.Data;
using AgilineeringApi.Models;
using AgilineeringApi.Utilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AgilineeringApi.Services;

public class PostsService(AppDbContext db, ILogger<PostsService> logger) : IPostsService
{
    private const int MaxTitleLength = 300;
    private const int MaxSlugLength = 300;
    private const int MaxContentLength = 500_000;

    public async Task<PagedResult<PostSummaryResponse>> GetAllAsync(bool includeUnpublished = false, int page = 1, int pageSize = 10, string? tag = null, CancellationToken ct = default)
    {
        var query = db.Posts
            .AsNoTracking()
            .Include(p => p.Author)
            .Include(p => p.Tags)
            .Where(p => includeUnpublished || p.Published);

        if (tag is not null)
            query = query.Where(p => p.Tags.Any(t => t.Slug == tag));

        var totalCount = await query.CountAsync(ct);

        var posts = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var postIds = posts.Where(p => !p.Published).Select(p => p.Id).ToList();
        var commentCounts = postIds.Count > 0
            ? await db.PreviewComments
                .Join(db.PostPreviews, c => c.PreviewId, pp => pp.Id, (c, pp) => pp.PostId)
                .Where(postId => postIds.Contains(postId))
                .GroupBy(postId => postId)
                .Select(g => new { PostId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.PostId, x => x.Count, ct)
            : [];

        var allTagIds = posts.SelectMany(p => p.Tags.Select(t => t.Id)).Distinct();
        var dudeUrls = await GetDudeUrlsAsync(allTagIds, ct);

        var items = posts
            .Select(p => new PostSummaryResponse(
                p.Id, p.Title, p.Slug, p.Published, p.CreatedAt,
                p.Author.Username,
                p.Tags.Select(t => new TagResponse(t.Id, t.Name, t.Slug, dudeUrls.GetValueOrDefault(t.Id))).ToList(),
                commentCounts.GetValueOrDefault(p.Id)))
            .ToList();

        return new PagedResult<PostSummaryResponse>(items, page, pageSize, totalCount);
    }

    public async Task<ServiceResult<PostDetailResponse>> GetBySlugAsync(string slug, bool includeUnpublished = false, CancellationToken ct = default)
    {
        var post = await db.Posts
            .AsNoTracking()
            .Include(p => p.Author)
            .Include(p => p.Tags)
            .FirstOrDefaultAsync(p => p.Slug == slug && (includeUnpublished || p.Published), ct);

        if (post is null)
            return ServiceResult<PostDetailResponse>.NotFound($"Post '{slug}' not found.");

        var dudeUrls = await GetDudeUrlsAsync(post.Tags.Select(t => t.Id), ct);
        return ServiceResult<PostDetailResponse>.Ok(ToDetail(post, dudeUrls));
    }

    public async Task<ServiceResult<PostDetailResponse>> CreateAsync(CreatePostRequest request, int authorId, CancellationToken ct = default)
    {
        var validation = ValidateFields(request.Title, request.Content, request.Slug);
        if (validation is not null) return validation;

        if (await db.Posts.AnyAsync(p => p.Slug == request.Slug, ct))
            return ServiceResult<PostDetailResponse>.Conflict($"Post with slug '{request.Slug}' already exists.");

        var requestedTagIds = (request.TagIds ?? []).ToList();
        var tags = await db.Tags.Where(t => requestedTagIds.Contains(t.Id)).ToListAsync(ct);
        var missingIds = requestedTagIds.Except(tags.Select(t => t.Id)).ToList();
        if (missingIds.Count > 0)
            return ServiceResult<PostDetailResponse>.BadRequest($"Tag IDs not found: {string.Join(", ", missingIds)}.");

        var author = await db.Users.FirstOrDefaultAsync(u => u.Id == authorId, ct);
        if (author is null)
            return ServiceResult<PostDetailResponse>.BadRequest($"Author {authorId} not found.");
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
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqliteException { SqliteErrorCode: SqliteErrorCodes.UniqueConstraintViolation })
        {
            return ServiceResult<PostDetailResponse>.Conflict($"Post with slug '{request.Slug}' already exists.");
        }
        return ServiceResult<PostDetailResponse>.Ok(ToDetail(post));
    }

    public async Task<ServiceResult<PostDetailResponse>> UpdateAsync(int id, UpdatePostRequest request, CancellationToken ct = default)
    {
        var validation = ValidateFields(request.Title, request.Content, request.Slug);
        if (validation is not null) return validation;

        var post = await db.Posts
            .Include(p => p.Author)
            .Include(p => p.Tags)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        if (post is null)
            return ServiceResult<PostDetailResponse>.NotFound($"Post {id} not found.");

        if (post.Slug != request.Slug && await db.Posts.AnyAsync(p => p.Slug == request.Slug, ct))
            return ServiceResult<PostDetailResponse>.Conflict($"Post with slug '{request.Slug}' already exists.");

        post.Title = request.Title;
        post.Content = request.Content;
        post.Slug = request.Slug;
        post.Published = request.Published;
        post.UpdatedAt = DateTime.UtcNow;
        var updatedTagIds = (request.TagIds ?? []).ToList();
        var updatedTags = await db.Tags.Where(t => updatedTagIds.Contains(t.Id)).ToListAsync(ct);
        var missingTagIds = updatedTagIds.Except(updatedTags.Select(t => t.Id)).ToList();
        if (missingTagIds.Count > 0)
            return ServiceResult<PostDetailResponse>.BadRequest($"Tag IDs not found: {string.Join(", ", missingTagIds)}.");

        post.Tags = updatedTags;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqliteException { SqliteErrorCode: SqliteErrorCodes.UniqueConstraintViolation })
        {
            return ServiceResult<PostDetailResponse>.Conflict($"Post with slug '{request.Slug}' already exists.");
        }
        return ServiceResult<PostDetailResponse>.Ok(ToDetail(post));
    }

    public async Task<ServiceResult> DeleteAsync(int id, CancellationToken ct = default)
    {
        var post = await db.Posts.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (post is null)
            return ServiceResult.NotFound($"Post {id} not found.");

        db.Posts.Remove(post);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Post {Id} ({Slug}) deleted", post.Id, post.Slug);
        return ServiceResult.Ok();
    }

    private static ServiceResult<PostDetailResponse>? ValidateFields(string title, string content, string slug)
    {
        if (string.IsNullOrWhiteSpace(title))
            return ServiceResult<PostDetailResponse>.BadRequest("Title is required.");
        if (title.Length > MaxTitleLength)
            return ServiceResult<PostDetailResponse>.BadRequest($"Title must be {MaxTitleLength} characters or fewer.");
        if (string.IsNullOrWhiteSpace(content))
            return ServiceResult<PostDetailResponse>.BadRequest("Content is required.");
        if (content.Length > MaxContentLength)
            return ServiceResult<PostDetailResponse>.BadRequest($"Content must be {MaxContentLength:N0} characters or fewer.");
        if (string.IsNullOrWhiteSpace(slug))
            return ServiceResult<PostDetailResponse>.BadRequest("Slug is required.");
        if (slug.Length > MaxSlugLength)
            return ServiceResult<PostDetailResponse>.BadRequest($"Slug must be {MaxSlugLength} characters or fewer.");
        if (!SlugValidator.IsValid(slug))
            return ServiceResult<PostDetailResponse>.BadRequest("Slug must contain only lowercase letters, numbers, and hyphens.");
        return null;
    }

    private async Task<Dictionary<int, string>> GetDudeUrlsAsync(IEnumerable<int> tagIds, CancellationToken ct)
    {
        var ids = tagIds.ToList();
        if (ids.Count == 0) return [];
        var images = await db.Images
            .Where(i => i.TagId != null && ids.Contains(i.TagId!.Value))
            .Select(i => new { i.TagId, i.Filename, i.CreatedAt })
            .ToListAsync(ct);
        return images
            .GroupBy(i => i.TagId!.Value)
            .ToDictionary(g => g.Key, g => "/images/" + g.OrderByDescending(i => i.CreatedAt).First().Filename);
    }

    private static PostDetailResponse ToDetail(Post post, Dictionary<int, string>? dudeUrls = null) => new(
        post.Id, post.Title, post.Content, post.Slug, post.Published,
        post.CreatedAt, post.UpdatedAt,
        post.Author.Username,
        post.Tags.Select(t => new TagResponse(t.Id, t.Name, t.Slug, dudeUrls?.GetValueOrDefault(t.Id))));
}
