using AgilineeringApi;
using AgilineeringApi.Data;
using AgilineeringApi.Models;
using Microsoft.EntityFrameworkCore;

namespace AgilineeringApi.Services;

public class PostPreviewService(AppDbContext db, ILogger<PostPreviewService> logger) : IPostPreviewService
{
    private const int MaxPreviewsPerPost = 20;
    private const int MaxCommentsPerPreview = 100;

    public async Task<ServiceResult<PreviewResponse>> CreateAsync(int postId, CreatePreviewRequest request, CancellationToken ct = default)
    {
        var post = await db.Posts.FirstOrDefaultAsync(p => p.Id == postId, ct);
        if (post is null)
            return ServiceResult<PreviewResponse>.NotFound("Post not found.");
        if (post.Published)
            return ServiceResult<PreviewResponse>.BadRequest("Published posts do not need preview links.");

        var previewCount = await db.PostPreviews.CountAsync(pp => pp.PostId == postId, ct);
        if (previewCount >= MaxPreviewsPerPost)
            return ServiceResult<PreviewResponse>.BadRequest($"This post already has the maximum number of previews ({MaxPreviewsPerPost}).");

        var preview = new PostPreview
        {
            PostId = postId,
            Token = Guid.NewGuid().ToString("N"),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, workFactor: SecurityConstants.PasswordHashWorkFactor),
            CreatedAt = DateTime.UtcNow
        };
        db.PostPreviews.Add(preview);
        await db.SaveChangesAsync(ct);
        return ServiceResult<PreviewResponse>.Ok(ToResponse(preview));
    }

    public async Task<bool> TokenExistsAsync(string token, CancellationToken ct = default) =>
        await db.PostPreviews.AnyAsync(pp => pp.Token == token, ct);

    public async Task<ServiceResult<PostDetailResponse>> AccessAsync(string token, PreviewAccessRequest request, CancellationToken ct = default)
    {
        var preview = await db.PostPreviews
            .Include(pp => pp.Post).ThenInclude(p => p.Author)
            .Include(pp => pp.Post).ThenInclude(p => p.Tags)
            .FirstOrDefaultAsync(pp => pp.Token == token, ct);

        if (preview is null)
            return ServiceResult<PostDetailResponse>.NotFound("Preview not found.");

        if (!VerifyCredentials(preview, request.Password))
        {
            logger.LogWarning("Failed preview access attempt for token {Token}", token);
            return ServiceResult<PostDetailResponse>.Forbidden("Invalid credentials.");
        }

        preview.LastAccessedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Preview accessed for token {Token}", token);
        var post = preview.Post;
        if (post is null)
            return ServiceResult<PostDetailResponse>.NotFound("The post associated with this preview no longer exists.");
        return ServiceResult<PostDetailResponse>.Ok(new PostDetailResponse(
            post.Id, post.Title, post.Content, post.Slug, post.Published,
            post.CreatedAt, post.UpdatedAt, post.Author.Username,
            post.Tags.Select(t => new TagResponse(t.Id, t.Name, t.Slug))));
    }

    public async Task<ServiceResult<CommentResponse>> AddCommentAsync(string token, CreateCommentRequest request, CancellationToken ct = default)
    {
        var preview = await db.PostPreviews.AsNoTracking().FirstOrDefaultAsync(pp => pp.Token == token, ct);
        if (preview is null)
            return ServiceResult<CommentResponse>.NotFound("Preview not found.");

        if (!VerifyCredentials(preview, request.Password))
        {
            logger.LogWarning("Failed comment attempt for token {Token}", token);
            return ServiceResult<CommentResponse>.Forbidden("Invalid credentials.");
        }

        var commentCount = await db.PreviewComments.CountAsync(c => c.PreviewId == preview.Id, ct);
        if (commentCount >= MaxCommentsPerPreview)
            return ServiceResult<CommentResponse>.BadRequest($"This preview has reached the maximum number of comments ({MaxCommentsPerPreview}).");

        var comment = new PreviewComment
        {
            PreviewId = preview.Id,
            Body = request.Body,
            CreatedAt = DateTime.UtcNow
        };
        db.PreviewComments.Add(comment);
        await db.SaveChangesAsync(ct);
        return ServiceResult<CommentResponse>.Ok(new CommentResponse(comment.Id, comment.Body, comment.CreatedAt));
    }

    public async Task<ServiceResult<IEnumerable<CommentResponse>>> GetCommentsAsync(string token, PreviewAccessRequest request, CancellationToken ct = default)
    {
        var preview = await db.PostPreviews.AsNoTracking().FirstOrDefaultAsync(pp => pp.Token == token, ct);
        if (preview is null)
            return ServiceResult<IEnumerable<CommentResponse>>.NotFound("Preview not found.");

        if (!VerifyCredentials(preview, request.Password))
        {
            logger.LogWarning("Failed comments list attempt for token {Token}", token);
            return ServiceResult<IEnumerable<CommentResponse>>.Forbidden("Invalid credentials.");
        }

        var comments = await db.PreviewComments
            .Where(c => c.PreviewId == preview.Id)
            .OrderBy(c => c.CreatedAt)
            .Take(MaxCommentsPerPreview)
            .Select(c => new CommentResponse(c.Id, c.Body, c.CreatedAt))
            .ToListAsync(ct);
        return ServiceResult<IEnumerable<CommentResponse>>.Ok(comments);
    }

    public async Task<IEnumerable<PreviewWithCommentsResponse>> GetAllWithCommentsAsync(int postId, CancellationToken ct = default)
    {
        var previews = await db.PostPreviews
            .AsNoTracking()
            .Where(pp => pp.PostId == postId)
            .OrderByDescending(pp => pp.CreatedAt)
            .ToListAsync(ct);

        var previewIds = previews.Select(pp => pp.Id).ToList();
        var comments = await db.PreviewComments
            .AsNoTracking()
            .Where(c => previewIds.Contains(c.PreviewId))
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(ct);

        var commentsLookup = comments.ToLookup(c => c.PreviewId);
        return previews.Select(pp => new PreviewWithCommentsResponse(
            pp.Id, pp.Token, pp.CreatedAt, pp.LastAccessedAt,
            commentsLookup[pp.Id].Select(c => new CommentResponse(c.Id, c.Body, c.CreatedAt))));
    }

    private static bool VerifyCredentials(PostPreview preview, string password) =>
        BCrypt.Net.BCrypt.Verify(password, preview.PasswordHash);

    private static PreviewResponse ToResponse(PostPreview pp) =>
        new(pp.Id, pp.Token, pp.CreatedAt);
}
