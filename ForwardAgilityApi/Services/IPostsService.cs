namespace ForwardAgilityApi.Services;

public record PostSummaryResponse(int Id, string Title, string Slug, bool Published, DateTime CreatedAt, string AuthorUsername, IEnumerable<TagResponse> Tags);
public record PostDetailResponse(int Id, string Title, string Content, string Slug, bool Published, DateTime CreatedAt, DateTime UpdatedAt, string AuthorUsername, IEnumerable<TagResponse> Tags);
public record CreatePostRequest(string Title, string Content, string Slug, bool Published, IEnumerable<int> TagIds);
public record UpdatePostRequest(string Title, string Content, string Slug, bool Published, IEnumerable<int> TagIds);

public interface IPostsService
{
    Task<List<PostSummaryResponse>> GetAllAsync(bool includeUnpublished = false);
    Task<ServiceResult<PostDetailResponse>> GetBySlugAsync(string slug, bool includeUnpublished = false);
    Task<ServiceResult<PostDetailResponse>> CreateAsync(CreatePostRequest request, int authorId);
    Task<ServiceResult<PostDetailResponse>> UpdateAsync(int id, UpdatePostRequest request);
    Task<ServiceResult> DeleteAsync(int id);
}
