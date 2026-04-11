namespace AgilineeringApi.Services;

public interface IPostPreviewService
{
    Task<ServiceResult<PreviewResponse>> CreateAsync(int postId, CreatePreviewRequest request, CancellationToken ct = default);
    Task<bool> TokenExistsAsync(string token, CancellationToken ct = default);
    Task<ServiceResult<PostDetailResponse>> AccessAsync(string token, PreviewAccessRequest request, CancellationToken ct = default);
    Task<ServiceResult<CommentResponse>> AddCommentAsync(string token, CreateCommentRequest request, CancellationToken ct = default);
    Task<ServiceResult<IEnumerable<CommentResponse>>> GetCommentsAsync(string token, PreviewAccessRequest request, CancellationToken ct = default);
    Task<IEnumerable<PreviewWithCommentsResponse>> GetAllWithCommentsAsync(int postId, CancellationToken ct = default);
}
