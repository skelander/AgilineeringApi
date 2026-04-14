using Microsoft.Extensions.Options;

namespace AgilineeringApi.Options;

public class SecurityOptionsValidator : IValidateOptions<SecurityOptions>
{
    public ValidateOptionsResult Validate(string? name, SecurityOptions options)
    {
        var errors = new List<string>();

        if (options.MaxFailedLoginAttempts <= 0)
            errors.Add("Security:MaxFailedLoginAttempts must be greater than 0.");
        if (options.LockoutDurationMinutes <= 0)
            errors.Add("Security:LockoutDurationMinutes must be greater than 0.");
        if (options.MinPasswordLength < 6)
            errors.Add("Security:MinPasswordLength must be at least 6.");

        return errors.Count > 0
            ? ValidateOptionsResult.Fail(errors)
            : ValidateOptionsResult.Success;
    }
}
