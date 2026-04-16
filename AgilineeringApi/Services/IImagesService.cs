using Microsoft.AspNetCore.Http;

namespace AgilineeringApi.Services;

public record ImageListItem(string Filename, string OriginalFilename, string Url, long Size, DateTime CreatedAt, int? TagId, string? TagName);
public record UploadedImageResponse(string Url);
public record SetImageTagRequest(int? TagId);

public interface IImagesService
{
    Task<IEnumerable<ImageListItem>> ListAsync(CancellationToken ct = default);
    Task<(byte[] Data, string ContentType)?> GetAsync(string filename, CancellationToken ct = default);
    Task<ServiceResult<string>> UploadAsync(IFormFile file, CancellationToken ct = default);
    Task<ServiceResult> DeleteAsync(string filename, CancellationToken ct = default);
    Task<ServiceResult> SetTagAsync(string filename, int? tagId, CancellationToken ct = default);
}
