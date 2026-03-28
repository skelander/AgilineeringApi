namespace ForwardAgilityApi.Services;

public record CreatePreviewRequest(string Name, string Password);
public record PreviewResponse(int Id, string Token, string Name, DateTime CreatedAt);
public record PreviewAccessRequest(string Name, string Password);

public interface IPostPreviewService
{
    Task<ServiceResult<PreviewResponse>> CreateAsync(int postId, CreatePreviewRequest request);
    Task<bool> TokenExistsAsync(string token);
    Task<ServiceResult<PostDetailResponse>> AccessAsync(string token, PreviewAccessRequest request);
}
