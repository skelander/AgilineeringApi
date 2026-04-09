using AgilineeringApi.Data;
using AgilineeringApi.Models;
using Microsoft.EntityFrameworkCore;

namespace AgilineeringApi.Services;

public class PostPreviewService(AppDbContext db, ILogger<PostPreviewService> logger) : IPostPreviewService
{
    public async Task<ServiceResult<PreviewResponse>> CreateAsync(int postId, CreatePreviewRequest request)
    {
        var post = await db.Posts.FindAsync(postId);
        if (post is null)
            return ServiceResult<PreviewResponse>.NotFound("Post not found.");
        if (post.Published)
            return ServiceResult<PreviewResponse>.BadRequest("Published posts do not need preview links.");

        var previewCount = await db.PostPreviews.CountAsync(pp => pp.PostId == postId);
        if (previewCount >= 20)
            return ServiceResult<PreviewResponse>.BadRequest("This post already has the maximum number of previews (20).");

        var preview = new PostPreview
        {
            PostId = postId,
            Token = Guid.NewGuid().ToString("N"),
            Name = request.Name,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, workFactor: 12),
            CreatedAt = DateTime.UtcNow
        };
        db.PostPreviews.Add(preview);
        await db.SaveChangesAsync();
        return ServiceResult<PreviewResponse>.Ok(ToResponse(preview));
    }

    public async Task<bool> TokenExistsAsync(string token) =>
        await db.PostPreviews.AnyAsync(pp => pp.Token == token);

    public async Task<ServiceResult<PostDetailResponse>> AccessAsync(string token, PreviewAccessRequest request)
    {
        var preview = await db.PostPreviews
            .AsNoTracking()
            .Include(pp => pp.Post).ThenInclude(p => p.Author)
            .Include(pp => pp.Post).ThenInclude(p => p.Tags)
            .FirstOrDefaultAsync(pp => pp.Token == token);

        if (preview is null)
            return ServiceResult<PostDetailResponse>.NotFound("Preview not found.");

        if (!VerifyCredentials(preview, request.Name, request.Password))
        {
            logger.LogWarning("Failed preview access attempt for token {Token} by name {Name}", token, request.Name);
            return ServiceResult<PostDetailResponse>.Forbidden("Invalid credentials.");
        }

        logger.LogInformation("Preview accessed for token {Token} by {Name}", token, request.Name);
        var post = preview.Post;
        if (post is null)
            return ServiceResult<PostDetailResponse>.NotFound("The post associated with this preview no longer exists.");
        return ServiceResult<PostDetailResponse>.Ok(new PostDetailResponse(
            post.Id, post.Title, post.Content, post.Slug, post.Published,
            post.CreatedAt, post.UpdatedAt, post.Author.Username,
            post.Tags.Select(t => new TagResponse(t.Id, t.Name, t.Slug))));
    }

    public async Task<ServiceResult<CommentResponse>> AddCommentAsync(string token, CreateCommentRequest request)
    {
        var preview = await db.PostPreviews.AsNoTracking().FirstOrDefaultAsync(pp => pp.Token == token);
        if (preview is null)
            return ServiceResult<CommentResponse>.NotFound("Preview not found.");

        if (!VerifyCredentials(preview, request.Name, request.Password))
        {
            logger.LogWarning("Failed comment attempt for token {Token} by name {Name}", token, request.Name);
            return ServiceResult<CommentResponse>.Forbidden("Invalid credentials.");
        }

        var comment = new PreviewComment
        {
            PreviewId = preview.Id,
            Body = request.Body,
            CreatedAt = DateTime.UtcNow
        };
        db.PreviewComments.Add(comment);
        await db.SaveChangesAsync();
        return ServiceResult<CommentResponse>.Ok(new CommentResponse(comment.Id, comment.Body, comment.CreatedAt));
    }

    public async Task<ServiceResult<IEnumerable<CommentResponse>>> GetCommentsAsync(string token, PreviewAccessRequest request)
    {
        var preview = await db.PostPreviews.AsNoTracking().FirstOrDefaultAsync(pp => pp.Token == token);
        if (preview is null)
            return ServiceResult<IEnumerable<CommentResponse>>.NotFound("Preview not found.");

        if (!VerifyCredentials(preview, request.Name, request.Password))
        {
            logger.LogWarning("Failed comments list attempt for token {Token} by name {Name}", token, request.Name);
            return ServiceResult<IEnumerable<CommentResponse>>.Forbidden("Invalid credentials.");
        }

        var comments = await db.PreviewComments
            .Where(c => c.PreviewId == preview.Id)
            .OrderBy(c => c.CreatedAt)
            .Take(100)
            .Select(c => new CommentResponse(c.Id, c.Body, c.CreatedAt))
            .ToListAsync();
        return ServiceResult<IEnumerable<CommentResponse>>.Ok(comments);
    }

    // Always verify both fields to avoid timing side-channel leaking which field was wrong
    private static bool VerifyCredentials(PostPreview preview, string name, string password) =>
        string.Equals(preview.Name, name, StringComparison.OrdinalIgnoreCase) &
        BCrypt.Net.BCrypt.Verify(password, preview.PasswordHash);

    private static PreviewResponse ToResponse(PostPreview pp) =>
        new(pp.Id, pp.Token, pp.Name, pp.CreatedAt);
}
