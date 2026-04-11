namespace AgilineeringApi.Services;

public interface IPostsService
{
    Task<PagedResult<PostSummaryResponse>> GetAllAsync(bool includeUnpublished = false, int page = 1, int pageSize = 10, string? tag = null, CancellationToken ct = default);
    Task<ServiceResult<PostDetailResponse>> GetBySlugAsync(string slug, bool includeUnpublished = false, CancellationToken ct = default);
    Task<ServiceResult<PostDetailResponse>> CreateAsync(CreatePostRequest request, int authorId, CancellationToken ct = default);
    Task<ServiceResult<PostDetailResponse>> UpdateAsync(int id, UpdatePostRequest request, CancellationToken ct = default);
    Task<ServiceResult> DeleteAsync(int id, CancellationToken ct = default);
}
