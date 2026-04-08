namespace AgilineeringApi.Services;

public record CreatePreviewRequest(string Name, string Password);
public record PreviewResponse(int Id, string Token, string Name, DateTime CreatedAt);
public record PreviewAccessRequest(string Name, string Password);
public record CreateCommentRequest(string Name, string Password, string Body);
public record CommentResponse(int Id, string Body, DateTime CreatedAt);

public interface IPostPreviewService
{
    Task<ServiceResult<PreviewResponse>> CreateAsync(int postId, CreatePreviewRequest request);
    Task<bool> TokenExistsAsync(string token);
    Task<ServiceResult<PostDetailResponse>> AccessAsync(string token, PreviewAccessRequest request);
    Task<ServiceResult<CommentResponse>> AddCommentAsync(string token, CreateCommentRequest request);
    Task<ServiceResult<IEnumerable<CommentResponse>>> GetCommentsAsync(string token, PreviewAccessRequest request);
}
