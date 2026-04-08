using AgilineeringApi.Data;
using AgilineeringApi.Models;
using Microsoft.EntityFrameworkCore;

namespace AgilineeringApi.Services;

public class PostPreviewService(AppDbContext db) : IPostPreviewService
{
    public async Task<ServiceResult<PreviewResponse>> CreateAsync(int postId, CreatePreviewRequest request)
    {
        var post = await db.Posts.FindAsync(postId);
        if (post is null)
            return ServiceResult<PreviewResponse>.NotFound("Post not found.");
        if (post.Published)
            return ServiceResult<PreviewResponse>.BadRequest("Published posts do not need preview links.");

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

        // Always verify both name and password to avoid timing side-channel
        var nameMatch = string.Equals(preview.Name, request.Name, StringComparison.OrdinalIgnoreCase);
        var passwordMatch = BCrypt.Net.BCrypt.Verify(request.Password, preview.PasswordHash);
        if (!nameMatch || !passwordMatch)
            return ServiceResult<PostDetailResponse>.Forbidden("Invalid credentials.");

        var post = preview.Post;
        return ServiceResult<PostDetailResponse>.Ok(new PostDetailResponse(
            post.Id, post.Title, post.Content, post.Slug, post.Published,
            post.CreatedAt, post.UpdatedAt, post.Author.Username,
            post.Tags.Select(t => new TagResponse(t.Id, t.Name, t.Slug))));
    }

    public async Task<ServiceResult<CommentResponse>> AddCommentAsync(string token, CreateCommentRequest request)
    {
        var preview = await db.PostPreviews.FirstOrDefaultAsync(pp => pp.Token == token);
        if (preview is null)
            return ServiceResult<CommentResponse>.NotFound("Preview not found.");

        var nameMatch = string.Equals(preview.Name, request.Name, StringComparison.OrdinalIgnoreCase);
        var passwordMatch = BCrypt.Net.BCrypt.Verify(request.Password, preview.PasswordHash);
        if (!nameMatch || !passwordMatch)
            return ServiceResult<CommentResponse>.Forbidden("Invalid credentials.");

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
        var preview = await db.PostPreviews.FirstOrDefaultAsync(pp => pp.Token == token);
        if (preview is null)
            return ServiceResult<IEnumerable<CommentResponse>>.NotFound("Preview not found.");

        var nameMatch = string.Equals(preview.Name, request.Name, StringComparison.OrdinalIgnoreCase);
        var passwordMatch = BCrypt.Net.BCrypt.Verify(request.Password, preview.PasswordHash);
        if (!nameMatch || !passwordMatch)
            return ServiceResult<IEnumerable<CommentResponse>>.Forbidden("Invalid credentials.");

        var comments = await db.PreviewComments
            .Where(c => c.PreviewId == preview.Id)
            .OrderBy(c => c.CreatedAt)
            .Select(c => new CommentResponse(c.Id, c.Body, c.CreatedAt))
            .ToListAsync();
        return ServiceResult<IEnumerable<CommentResponse>>.Ok(comments);
    }

    private static PreviewResponse ToResponse(PostPreview pp) =>
        new(pp.Id, pp.Token, pp.Name, pp.CreatedAt);
}
