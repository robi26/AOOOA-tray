using System.Text;

namespace SST.AutoOutOfOfficeAssistant.Core;

/// <summary>
/// Converts a plain-text reply template (as edited in the tray app) to the HTML Exchange expects:
/// blank lines separate paragraphs, single line breaks become <c>&lt;br&gt;</c>. Placeholders such as
/// <c>{EndDate}</c> are kept, so the result can be rendered with <see cref="TemplateRenderer"/>.
/// </summary>
public static class PlainTextTemplate
{
    /// <summary>Gets the bilingual default template as plain text.</summary>
    public const string Default =
        "Vielen Dank für Ihre Nachricht. Ich bin bis und mit {EndDate} abwesend und ab {ReturnDate} wieder erreichbar. "
        + "Ihre E-Mail wird nicht weitergeleitet.\n\n"
        + "Thank you for your message. I am out of the office until {EndDate} and will be back on {ReturnDate}. "
        + "Your e-mail will not be forwarded.";

    /// <summary>Converts the text to HTML.</summary>
    /// <param name="text">The plain text.</param>
    /// <returns>HTML paragraphs; empty for empty input.</returns>
    public static string ToHtml(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        var paragraphs = normalized
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(p => p.Length > 0);

        var html = new StringBuilder();
        foreach (var paragraph in paragraphs)
        {
            var lines = paragraph.Split('\n').Select(line => Escape(line.TrimEnd()));
            html.Append("<p>").Append(string.Join("<br>", lines)).Append("</p>");
        }

        return html.ToString();
    }

    // Only the characters that matter in HTML text; umlauts etc. stay readable (WebUtility.HtmlEncode would turn them into entities).
    private static string Escape(string text) => text
        .Replace("&", "&amp;", StringComparison.Ordinal)
        .Replace("<", "&lt;", StringComparison.Ordinal)
        .Replace(">", "&gt;", StringComparison.Ordinal)
        .Replace("\"", "&quot;", StringComparison.Ordinal);
}
