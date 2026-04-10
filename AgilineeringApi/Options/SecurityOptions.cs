namespace AgilineeringApi.Options;

public class SecurityOptions
{
    public int MaxFailedLoginAttempts { get; init; } = 5;
    public int LockoutDurationMinutes { get; init; } = 15;
    public int MinPasswordLength { get; init; } = 12;
}
