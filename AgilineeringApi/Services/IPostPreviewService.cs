namespace AgilineeringApi.Services;

public interface IPostPreviewService
{
    Task<ServiceResult<PreviewResponse>> CreateAsync(int postId, CreatePreviewRequest request);
    Task<bool> TokenExistsAsync(string token);
    Task<ServiceResult<PostDetailResponse>> AccessAsync(string token, PreviewAccessRequest request);
    Task<ServiceResult<CommentResponse>> AddCommentAsync(string token, CreateCommentRequest request);
    Task<ServiceResult<IEnumerable<CommentResponse>>> GetCommentsAsync(string token, PreviewAccessRequest request);
}
