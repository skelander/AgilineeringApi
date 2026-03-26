namespace ForwardAgilityApi.Services;

public record PostSummaryResponse(int Id, string Title, string Slug, bool Published, DateTime CreatedAt, string AuthorUsername, IEnumerable<TagResponse> Tags);
public record PostDetailResponse(int Id, string Title, string Content, string Slug, bool Published, DateTime CreatedAt, DateTime UpdatedAt, string AuthorUsername, IEnumerable<TagResponse> Tags);
public record CreatePostRequest(string Title, string Content, string Slug, bool Published, IEnumerable<int> TagIds);
public record UpdatePostRequest(string Title, string Content, string Slug, bool Published, IEnumerable<int> TagIds);
public record PagedResult<T>(IEnumerable<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}

public interface IPostsService
{
    Task<PagedResult<PostSummaryResponse>> GetAllAsync(bool includeUnpublished = false, int page = 1, int pageSize = 10, string? tag = null);
    Task<ServiceResult<PostDetailResponse>> GetBySlugAsync(string slug, bool includeUnpublished = false);
    Task<ServiceResult<PostDetailResponse>> CreateAsync(CreatePostRequest request, int authorId);
    Task<ServiceResult<PostDetailResponse>> UpdateAsync(int id, UpdatePostRequest request);
    Task<ServiceResult> DeleteAsync(int id);
}
