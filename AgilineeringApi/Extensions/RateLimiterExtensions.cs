using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace AgilineeringApi.Extensions;

internal static class RateLimiterExtensions
{
    internal static IServiceCollection AddAgilineeringRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddRateLimiter(options =>
        {
            options.AddFixedWindowPolicy("read", configuration, "Security:ReadRateLimit", defaultLimit: 120);
            options.AddFixedWindowPolicy("login", configuration, "Security:LoginRateLimit", defaultLimit: 10);
            options.AddFixedWindowPolicy("write", configuration, "Security:WriteRateLimit", defaultLimit: 30);
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
            RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    Window = TimeSpan.FromMinutes(1),
                    PermitLimit = configuration.GetValue(configKey, defaultLimit),
                    QueueLimit = 0,
                }));
    }
}
