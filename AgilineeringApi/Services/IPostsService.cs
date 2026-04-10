namespace AgilineeringApi.Services;

public interface IPostsService
{
    Task<PagedResult<PostSummaryResponse>> GetAllAsync(bool includeUnpublished = false, int page = 1, int pageSize = 10, string? tag = null);
    Task<ServiceResult<PostDetailResponse>> GetBySlugAsync(string slug, bool includeUnpublished = false);
    Task<ServiceResult<PostDetailResponse>> CreateAsync(CreatePostRequest request, int authorId);
    Task<ServiceResult<PostDetailResponse>> UpdateAsync(int id, UpdatePostRequest request);
    Task<ServiceResult> DeleteAsync(int id);
}
