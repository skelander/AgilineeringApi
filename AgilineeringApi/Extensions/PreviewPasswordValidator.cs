using Microsoft.AspNetCore.Mvc;

namespace AgilineeringApi.Extensions;

internal static class PreviewPasswordValidator
{
    internal static IActionResult? Validate(string password, ControllerBase controller)
    {
        if (string.IsNullOrWhiteSpace(password))
            return controller.BadRequest(new { error = "Password is required." });
        if (password.Length < 6)
            return controller.BadRequest(new { error = "Password must be at least 6 characters." });
        if (password.Length > SecurityConstants.MaxPasswordLength)
            return controller.BadRequest(new { error = $"Password must be {SecurityConstants.MaxPasswordLength} characters or fewer." });
        return null;
    }
}
