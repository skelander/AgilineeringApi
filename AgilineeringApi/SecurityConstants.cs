namespace AgilineeringApi;

internal static class SecurityConstants
{
    internal const int PasswordHashWorkFactor = 12;
    // BCrypt silently truncates input at 72 bytes; cap well below that to prevent DoS.
    internal const int MaxPasswordLength = 1000;
    // Guid.NewGuid().ToString("N") always produces exactly this many hex characters.
    // AppDbContext.HasMaxLength must match this value.
    internal const int PreviewTokenLength = 32;
}
