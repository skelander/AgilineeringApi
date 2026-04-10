using Microsoft.AspNetCore.Http;

namespace AgilineeringApi.Services;

public record ImageListItem(string Filename, string Url, long Size, DateTime CreatedAt);

public interface IImagesService
{
    Task<IEnumerable<ImageListItem>> ListAsync();
    Task<(byte[] Data, string ContentType)?> GetAsync(string filename);
    Task<ServiceResult<string>> UploadAsync(IFormFile file);
    Task<ServiceResult> DeleteAsync(string filename);
}
