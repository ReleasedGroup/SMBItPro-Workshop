using System.Text.RegularExpressions;

namespace Helpdesk.Light.Infrastructure.Services;

internal static partial class TicketReferenceParser
{
    [GeneratedRegex(@"\[(HD-[A-F0-9]+)\]", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex ReferenceRegex();

    public static string? ExtractReferenceCode(string? subject)
    {
        if (string.IsNullOrWhiteSpace(subject))
        {
            return null;
        }

        Match match = ReferenceRegex().Match(subject);
        if (!match.Success)
        {
            return null;
        }

        return match.Groups[1].Value.ToUpperInvariant();
    }
}
