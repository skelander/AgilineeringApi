using System.Security.Cryptography;
using System.Text;

namespace AgilineeringApi.Infrastructure;

public class AdminKeyMiddleware(RequestDelegate next, IConfiguration configuration, ILogger<AdminKeyMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (IsWriteMethod(context.Request.Method) && !IsPublicWriteEndpoint(context.Request))
        {
            var configuredKey = configuration["AdminKey"];
            if (string.IsNullOrEmpty(configuredKey))
            {
                logger.LogWarning("Admin key is not configured — write request blocked: {Method} {Path}",
                    context.Request.Method, context.Request.Path);
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { error = "Write access is not available." });
                return;
            }
            var providedKey = context.Request.Headers["X-Admin-Key"].FirstOrDefault() ?? "";
            var configuredHash = SHA256.HashData(Encoding.UTF8.GetBytes(configuredKey));
            var providedHash = SHA256.HashData(Encoding.UTF8.GetBytes(providedKey));
            if (!CryptographicOperations.FixedTimeEquals(configuredHash, providedHash))
            {
                logger.LogWarning("Invalid admin key on {Method} {Path} from {IP}",
                    context.Request.Method, context.Request.Path,
                    context.Connection.RemoteIpAddress);
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { error = "Write access is not available." });
                return;
            }
        }
        await next(context);
    }

    private static bool IsWriteMethod(string method) =>
        HttpMethods.IsPost(method) || HttpMethods.IsPut(method) || HttpMethods.IsDelete(method);

    private static bool IsPublicWriteEndpoint(HttpRequest request)
    {
        var path = request.Path.Value ?? "";
        if (path.StartsWith("/auth/", StringComparison.OrdinalIgnoreCase)) return true;
        // Preview access endpoints are public POST — only POST, not DELETE/PUT
        if (HttpMethods.IsPost(request.Method) &&
            path.Contains("/preview/", StringComparison.OrdinalIgnoreCase) &&
            (path.EndsWith("/access", StringComparison.OrdinalIgnoreCase) ||
             path.EndsWith("/comments", StringComparison.OrdinalIgnoreCase) ||
             path.EndsWith("/comments/list", StringComparison.OrdinalIgnoreCase))) return true;
        return false;
    }
}
