namespace AgilineeringApi;

internal static class SecurityConstants
{
    internal const int PasswordHashWorkFactor = 12;
    // Guid.NewGuid().ToString("N") always produces exactly this many hex characters.
    // AppDbContext.HasMaxLength must match this value.
    internal const int PreviewTokenLength = 32;
}
