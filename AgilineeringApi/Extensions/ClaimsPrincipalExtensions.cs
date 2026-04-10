using System.Security.Claims;

namespace AgilineeringApi.Extensions;

internal static class ClaimsPrincipalExtensions
{
    internal static int? GetUserId(this ClaimsPrincipal user)
    {
        if (int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id))
            return id;
        return null;
    }
}
