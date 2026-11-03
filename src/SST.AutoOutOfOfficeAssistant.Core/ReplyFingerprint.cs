using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace SST.AutoOutOfOfficeAssistant.Core;

/// <summary>
/// A stable hash of an <see cref="AutomaticReplies"/> setting. It lets the app recognise a setting it wrote itself,
/// even though Exchange rewrites the HTML of the messages (it wraps them in its own document markup).
/// Messages are therefore compared as normalised plain text, and times at minute precision.
/// </summary>
public static partial class ReplyFingerprint
{
    /// <summary>Computes the fingerprint.</summary>
    /// <param name="replies">The setting.</param>
    /// <returns>A hex string.</returns>
    public static string Compute(AutomaticReplies replies)
    {
        ArgumentNullException.ThrowIfNull(replies);

        var canonical = string.Join(
            '|',
            replies.Status,
            replies.ExternalAudience,
            FormatTime(replies.ScheduledStart),
            FormatTime(replies.ScheduledEnd),
            NormalizeText(replies.InternalMessage),
            NormalizeText(replies.ExternalMessage));

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    /// <summary>Reduces HTML to its visible text with collapsed whitespace.</summary>
    /// <param name="html">The HTML, or null.</param>
    /// <returns>The normalised text.</returns>
    internal static string NormalizeText(string? html)
    {
        if (string.IsNullOrEmpty(html))
        {
            return string.Empty;
        }

        var text = InvisibleBlocks().Replace(html, " ");
        text = Tags().Replace(text, " ");
        text = WebUtility.HtmlDecode(text);
        text = Whitespace().Replace(text, " ");
        return text.Trim();
    }

    private static string FormatTime(DateTimeOffset? value) =>
        value is null ? string.Empty : value.Value.UtcDateTime.ToString("yyyyMMddHHmm", System.Globalization.CultureInfo.InvariantCulture);

    [GeneratedRegex(@"<(head|style|script)\b[^>]*>.*?</\1\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex InvisibleBlocks();

    [GeneratedRegex(@"<[^>]*>", RegexOptions.Singleline)]
    private static partial Regex Tags();

    [GeneratedRegex(@"[\s ]+")]
    private static partial Regex Whitespace();
}
