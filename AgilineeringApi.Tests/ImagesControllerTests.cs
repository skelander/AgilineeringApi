using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace AgilineeringApi.Tests;

public class ImagesControllerTests : IClassFixture<AgilineeringFactory>
{
    private readonly HttpClient _client;

    public ImagesControllerTests(AgilineeringFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Upload_Unauthenticated_Returns401()
    {
        await _client.LogoutAsync();
        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent([0xFF, 0xD8, 0xFF]), "file", "test.jpg");

        var response = await _client.PostAsync("/images", content);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Upload_ValidImage_Returns201WithUrl()
    {
        await _client.AuthenticateAsync();
        using var content = new MultipartFormDataContent();
        var imageBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10 };
        var fileContent = new ByteArrayContent(imageBytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
        content.Add(fileContent, "file", "photo.jpg");

        var response = await _client.PostAsync("/images", content);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<UploadImageResponse>();
        Assert.NotNull(body);
        Assert.StartsWith("/images/", body!.Url);
        Assert.EndsWith(".jpg", body.Url);
    }

    [Fact]
    public async Task Upload_NoFile_Returns400()
    {
        await _client.AuthenticateAsync();
        using var content = new MultipartFormDataContent();

        var response = await _client.PostAsync("/images", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Upload_InvalidExtension_Returns400()
    {
        await _client.AuthenticateAsync();
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent([0x00, 0x01, 0x02]);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
        content.Add(fileContent, "file", "malware.exe");

        var response = await _client.PostAsync("/images", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Upload_WavFileAsWebp_Returns400()
    {
        // WAV files start with RIFF but do NOT have "WEBP" at bytes 8-11
        await _client.AuthenticateAsync();
        using var content = new MultipartFormDataContent();
        // RIFF header + size + "WAVE" (not "WEBP")
        var wavBytes = new byte[] { 0x52, 0x49, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00, 0x57, 0x41, 0x56, 0x45 };
        var fileContent = new ByteArrayContent(wavBytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/webp");
        content.Add(fileContent, "file", "not-a-webp.webp");

        var response = await _client.PostAsync("/images", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Upload_ValidWebp_Returns201()
    {
        await _client.AuthenticateAsync();
        using var content = new MultipartFormDataContent();
        // RIFF + size + "WEBP"
        var webpBytes = new byte[] { 0x52, 0x49, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50 };
        var fileContent = new ByteArrayContent(webpBytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/webp");
        content.Add(fileContent, "file", "image.webp");

        var response = await _client.PostAsync("/images", content);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }
}

public class PostsContentLengthTests : IClassFixture<AgilineeringFactory>
{
    private readonly HttpClient _client;

    public PostsContentLengthTests(AgilineeringFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Create_ContentExceedsLimit_Returns400()
    {
        await _client.AuthenticateAsync();
        var response = await _client.PostAsJsonAsync("/posts", new
        {
            title = "Test",
            content = new string('x', 500_001),
            slug = "content-too-long",
            published = false,
            tagIds = Array.Empty<int>()
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_ContentAtLimit_Returns201()
    {
        await _client.AuthenticateAsync();
        var response = await _client.PostAsJsonAsync("/posts", new
        {
            title = "Test",
            content = new string('x', 500_000),
            slug = "content-at-limit",
            published = false,
            tagIds = Array.Empty<int>()
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }
}

record UploadImageResponse(string Url);
