using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace AgilineeringApi.Extensions;

internal static class RateLimiterExtensions
{
    private const int DefaultReadLimit = 120;   // requests per minute for read endpoints
    private const int DefaultLoginLimit = 10;   // requests per minute for auth endpoints
    private const int DefaultWriteLimit = 30;   // requests per minute for write endpoints

    internal static IServiceCollection AddAgilineeringRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddRateLimiter(options =>
        {
            options.AddFixedWindowPolicy("read", configuration, "Security:ReadRateLimit", defaultLimit: DefaultReadLimit);
            options.AddFixedWindowPolicy("login", configuration, "Security:LoginRateLimit", defaultLimit: DefaultLoginLimit);
            options.AddFixedWindowPolicy("write", configuration, "Security:WriteRateLimit", defaultLimit: DefaultWriteLimit);
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        });
        return services;
    }

    private static void AddFixedWindowPolicy(
        this RateLimiterOptions options,
        string policyName,
        IConfiguration configuration,
        string configKey,
        int defaultLimit)
    {
        options.AddPolicy(policyName, context =>
        {
            var ip = context.Connection.RemoteIpAddress?.ToString();
            if (ip is null)
            {
                var logger = context.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger(nameof(RateLimiterExtensions));
                logger.LogWarning(
                    "Rate limiter: RemoteIpAddress is null for policy {Policy} — request bucketed under 'unknown', which may bypass per-IP limiting",
                    policyName);
            }

            return RateLimitPartition.GetFixedWindowLimiter(
                ip ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    Window = TimeSpan.FromMinutes(1),
                    PermitLimit = configuration.GetValue(configKey, defaultLimit),
                    QueueLimit = 0,
                });
        });
    }
}
