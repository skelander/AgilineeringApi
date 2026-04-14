namespace AgilineeringApi;

internal static class SecurityConstants
{
    internal const int PasswordHashWorkFactor = 12;
    // BCrypt silently truncates input at 72 bytes, so passwords longer than 72 bytes are
    // equivalent. MaxPasswordLength is set much higher to prevent DoS via large BCrypt input,
    // not to enforce BCrypt correctness — the truncation is an accepted limitation.
    internal const int MaxPasswordLength = 1000;
    internal const int MaxUsernameLength = 200;
    internal const int MaxCommentBodyLength = 5000;
    // Guid.NewGuid().ToString("N") always produces exactly this many hex characters.
    // AppDbContext.HasMaxLength must match this value.
    internal const int PreviewTokenLength = 32;
    internal const int MaxSitemapUrls = 1000;
}
