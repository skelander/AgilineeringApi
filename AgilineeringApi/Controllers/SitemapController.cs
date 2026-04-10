using AgilineeringApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace AgilineeringApi.Controllers;

[ApiController]
public class SitemapController(IPostsService postsService, IConfiguration configuration) : ControllerBase
{
    [HttpGet("/sitemap.xml")]
    public async Task<IActionResult> Get()
    {
        var baseUrl = configuration["Site:BaseUrl"]?.TrimEnd('/');
        if (string.IsNullOrEmpty(baseUrl))
            return Problem("Site:BaseUrl is not configured.", statusCode: 500);
        var result = await postsService.GetAllAsync(includeUnpublished: false, page: 1, pageSize: 1000);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">");
        foreach (var post in result.Items)
        {
            var escapedSlug = System.Security.SecurityElement.Escape(post.Slug);
            var escapedBase = System.Security.SecurityElement.Escape(baseUrl);
            sb.AppendLine($"  <url><loc>{escapedBase}/post/{escapedSlug}</loc></url>");
        }
        sb.AppendLine("</urlset>");

        return Content(sb.ToString(), "application/xml");
    }
}
