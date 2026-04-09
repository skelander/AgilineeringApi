namespace AgilineeringApi.Services;

public record CreatePreviewRequest(string Password);
public record PreviewResponse(int Id, string Token, DateTime CreatedAt);
public record PreviewAccessRequest(string Password);
public record CreateCommentRequest(string Password, string Body);
public record CommentResponse(int Id, string Body, DateTime CreatedAt);

public interface IPostPreviewService
{
    Task<ServiceResult<PreviewResponse>> CreateAsync(int postId, CreatePreviewRequest request);
    Task<bool> TokenExistsAsync(string token);
    Task<ServiceResult<PostDetailResponse>> AccessAsync(string token, PreviewAccessRequest request);
    Task<ServiceResult<CommentResponse>> AddCommentAsync(string token, CreateCommentRequest request);
    Task<ServiceResult<IEnumerable<CommentResponse>>> GetCommentsAsync(string token, PreviewAccessRequest request);
}
