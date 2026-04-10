using AgilineeringApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace AgilineeringApi.Extensions;

internal static class ServiceResultExtensions
{
    internal static IActionResult ToActionResult<T>(
        this ServiceResult<T> result,
        ControllerBase ctrl,
        Func<T, IActionResult> onSuccess) =>
        result.Status switch
        {
            ServiceResultStatus.Ok => onSuccess(result.Value!),
            ServiceResultStatus.NotFound => ctrl.NotFound(new { error = result.Error }),
            ServiceResultStatus.Conflict => ctrl.Conflict(new { error = result.Error }),
            ServiceResultStatus.BadRequest => ctrl.BadRequest(new { error = result.Error }),
            ServiceResultStatus.Forbidden => ctrl.StatusCode(403, new { error = result.Error }),
            _ => ctrl.StatusCode(500)
        };

    internal static IActionResult ToActionResult(
        this ServiceResult result,
        ControllerBase ctrl,
        IActionResult onSuccess) =>
        result.Status switch
        {
            ServiceResultStatus.Ok => onSuccess,
            ServiceResultStatus.NotFound => ctrl.NotFound(new { error = result.Error }),
            ServiceResultStatus.Conflict => ctrl.Conflict(new { error = result.Error }),
            ServiceResultStatus.BadRequest => ctrl.BadRequest(new { error = result.Error }),
            ServiceResultStatus.Forbidden => ctrl.StatusCode(403, new { error = result.Error }),
            _ => ctrl.StatusCode(500)
        };
}
