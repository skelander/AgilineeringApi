using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace AgilineeringApi.Tests;

public class ImagesListServeDeleteTests : IClassFixture<AgilineeringFactory>
{
    private readonly HttpClient _client;

    public ImagesListServeDeleteTests(AgilineeringFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<string> UploadJpegAsync()
    {
        await _client.AuthenticateAsync();
        using var content = new MultipartFormDataContent();
        var imageBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10 };
        var fileContent = new ByteArrayContent(imageBytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
        content.Add(fileContent, "file", "photo.jpg");

        var response = await _client.PostAsync("/images", content);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<UploadImageResponse>();
        return body!.Url; // e.g. /images/abc123.jpg
    }

    // --- List ---

    [Fact]
    public async Task List_AsAdmin_ReturnsImages()
    {
        await UploadJpegAsync();

        var response = await _client.GetAsync("/images");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var images = await response.Content.ReadFromJsonAsync<List<ImageListItem>>();
        Assert.NotNull(images);
        Assert.NotEmpty(images);
        Assert.All(images, i =>
        {
            Assert.NotEmpty(i.Filename);
            Assert.StartsWith("/images/", i.Url);
            Assert.True(i.Size > 0);
        });
    }

    [Fact]
    public async Task List_Unauthenticated_Returns401()
    {
        await _client.LogoutAsync();

        var response = await _client.GetAsync("/images");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // --- Serve ---

    [Fact]
    public async Task Serve_ExistingImage_Returns200WithCorrectContentType()
    {
        var url = await UploadJpegAsync();

        var response = await _client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/jpeg", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Serve_ExistingImage_HasLongCacheHeader()
    {
        var url = await UploadJpegAsync();

        var response = await _client.GetAsync(url);
        var cacheControl = response.Headers.CacheControl;
        Assert.NotNull(cacheControl);
        Assert.True(cacheControl!.Public);
        Assert.True(cacheControl.MaxAge >= TimeSpan.FromDays(365));
    }

    [Fact]
    public async Task Serve_NonExistentImage_Returns404()
    {
        var response = await _client.GetAsync("/images/doesnotexist.jpg");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // --- Delete ---

    [Fact]
    public async Task Delete_ExistingImage_Returns204()
    {
        var url = await UploadJpegAsync();
        var filename = url.Split('/').Last();

        var response = await _client.DeleteAsync($"/images/{filename}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Delete_ExistingImage_ImageNoLongerServable()
    {
        var url = await UploadJpegAsync();
        var filename = url.Split('/').Last();

        await _client.DeleteAsync($"/images/{filename}");

        var serveResponse = await _client.GetAsync(url);
        Assert.Equal(HttpStatusCode.NotFound, serveResponse.StatusCode);
    }

    [Fact]
    public async Task Delete_NonExistentImage_Returns404()
    {
        await _client.AuthenticateAsync();

        var response = await _client.DeleteAsync("/images/doesnotexist.jpg");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_Unauthenticated_Returns401()
    {
        await _client.LogoutAsync();

        var response = await _client.DeleteAsync("/images/some.jpg");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("../secrets.txt")]
    [InlineData("../../etc/passwd")]
    [InlineData("..%2F..%2Fetc%2Fpasswd")]
    public async Task Serve_DirectoryTraversalFilename_IsBlocked(string filename)
    {
        // Path traversal attempts are neutralised either at URL-normalisation (→ 404)
        // or by the controller's safeFilename check (→ 400). Either way, never 200.
        var response = await _client.GetAsync($"/images/{filename}");
        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("../secrets.txt")]
    [InlineData("..%2F..%2Fetc%2Fpasswd")]
    public async Task Delete_DirectoryTraversalFilename_IsBlocked(string filename)
    {
        await _client.AuthenticateAsync();
        var response = await _client.DeleteAsync($"/images/{filename}");
        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Upload_FileSizeLimit_Returns400()
    {
        await _client.AuthenticateAsync();
        using var content = new MultipartFormDataContent();
        // Build a fake 10MB+1 byte JPEG (starts with valid magic bytes)
        var tooLarge = new byte[10 * 1024 * 1024 + 1];
        tooLarge[0] = 0xFF; tooLarge[1] = 0xD8; tooLarge[2] = 0xFF;
        var fileContent = new ByteArrayContent(tooLarge);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
        content.Add(fileContent, "file", "toolarge.jpg");

        var response = await _client.PostAsync("/images", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Upload_Gif87a_Returns201()
    {
        await _client.AuthenticateAsync();
        using var content = new MultipartFormDataContent();
        var gif87 = new byte[] { 0x47, 0x49, 0x46, 0x38, 0x37, 0x61, 0x01, 0x00, 0x01, 0x00 };
        var fileContent = new ByteArrayContent(gif87);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/gif");
        content.Add(fileContent, "file", "image.gif");

        var response = await _client.PostAsync("/images", content);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Upload_Gif89a_Returns201()
    {
        await _client.AuthenticateAsync();
        using var content = new MultipartFormDataContent();
        var gif89 = new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 0x01, 0x00, 0x01, 0x00 };
        var fileContent = new ByteArrayContent(gif89);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/gif");
        content.Add(fileContent, "file", "image.gif");

        var response = await _client.PostAsync("/images", content);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }
}

record ImageListItem(string Filename, string Url, long Size, string CreatedAt);
