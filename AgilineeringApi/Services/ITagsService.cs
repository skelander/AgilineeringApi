namespace AgilineeringApi.Services;

public interface ITagsService
{
    Task<List<TagResponse>> GetAllAsync();
    Task<ServiceResult<TagResponse>> CreateAsync(CreateTagRequest request);
    Task<ServiceResult> DeleteAsync(int id);
}
