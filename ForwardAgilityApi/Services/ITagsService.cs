using ForwardAgilityApi.Models;

namespace ForwardAgilityApi.Services;

public record TagResponse(int Id, string Name, string Slug);
public record CreateTagRequest(string Name, string Slug);

public interface ITagsService
{
    Task<List<TagResponse>> GetAllAsync();
    Task<ServiceResult<TagResponse>> CreateAsync(CreateTagRequest request);
    Task<ServiceResult> DeleteAsync(int id);
}
