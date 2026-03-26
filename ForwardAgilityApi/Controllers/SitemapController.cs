using ForwardAgilityApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace ForwardAgilityApi.Controllers;

[ApiController]
public class SitemapController(IPostsService postsService, IConfiguration configuration) : ControllerBase
{
    [HttpGet("/sitemap.xml")]
    public async Task<IActionResult> Get()
    {
        var baseUrl = (configuration["Site:BaseUrl"] ?? "").TrimEnd('/');
        var result = await postsService.GetAllAsync(includeUnpublished: false, page: 1, pageSize: 1000);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">");
        foreach (var post in result.Items)
        {
            sb.AppendLine($"  <url><loc>{baseUrl}/#/post/{post.Slug}</loc></url>");
        }
        sb.AppendLine("</urlset>");

        return Content(sb.ToString(), "application/xml");
    }
}
