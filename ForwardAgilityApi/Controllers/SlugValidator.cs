using System.Text.RegularExpressions;

namespace ForwardAgilityApi.Controllers;

internal static class SlugValidator
{
    private static readonly Regex Pattern =
        new(@"^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.Compiled);

    public static bool IsValid(string slug) => Pattern.IsMatch(slug);
}
