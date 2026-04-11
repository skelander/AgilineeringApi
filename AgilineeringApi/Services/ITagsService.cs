namespace AgilineeringApi.Services;

public interface ITagsService
{
    Task<List<TagResponse>> GetAllAsync(CancellationToken ct = default);
    Task<ServiceResult<TagResponse>> CreateAsync(CreateTagRequest request, CancellationToken ct = default);
    Task<ServiceResult> DeleteAsync(int id, CancellationToken ct = default);
}
