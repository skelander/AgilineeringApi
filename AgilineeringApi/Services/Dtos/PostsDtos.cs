namespace AgilineeringApi.Services;

public record PostSummaryResponse(int Id, string Title, string Slug, bool Published, DateTime CreatedAt, string AuthorUsername, IEnumerable<TagResponse> Tags);
public record PostDetailResponse(int Id, string Title, string Content, string Slug, bool Published, DateTime CreatedAt, DateTime UpdatedAt, string AuthorUsername, IEnumerable<TagResponse> Tags);
public record CreatePostRequest(string Title, string Content, string Slug, bool Published, IEnumerable<int>? TagIds = null);
public record UpdatePostRequest(string Title, string Content, string Slug, bool Published, IEnumerable<int>? TagIds = null);
public record PagedResult<T>(IEnumerable<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}
