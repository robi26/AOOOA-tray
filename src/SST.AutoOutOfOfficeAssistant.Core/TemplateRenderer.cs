using System.Globalization;
using System.Text.RegularExpressions;

namespace SST.AutoOutOfOfficeAssistant.Core;

/// <summary>
/// Fills the placeholders of a reply template with the dates of an absence block.
/// </summary>
public static partial class TemplateRenderer
{
    /// <summary>The supported placeholders and what they stand for (German, shown in the UI).</summary>
    public static IReadOnlyList<(string Placeholder, string Description)> Placeholders { get; } =
    [
        ("{StartDate}", "Erster Tag der Abwesenheit"),
        ("{StartDateTime}", "Beginn der Abwesenheit (Datum und Zeit)"),
        ("{EndDate}", "Letzter Tag der Abwesenheit"),
        ("{EndDateTime}", "Ende der Abwesenheit (Datum und Zeit)"),
        ("{ReturnDate}", "Tag, an dem Sie wieder arbeiten"),
        ("{ReturnDateTime}", "Zeitpunkt, ab dem Sie wieder arbeiten"),
    ];

    /// <summary>Renders the template.</summary>
    /// <param name="template">The template text (HTML). Unknown placeholders are left as they are.</param>
    /// <param name="block">The absence block.</param>
    /// <param name="timeZone">The time zone the dates are shown in.</param>
    /// <param name="culture">The culture used to format the dates.</param>
    /// <returns>The rendered text.</returns>
    public static string Render(string template, AbsenceBlock block, TimeZoneInfo timeZone, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(block);
        ArgumentNullException.ThrowIfNull(timeZone);
        ArgumentNullException.ThrowIfNull(culture);

        DateTime Local(DateTimeOffset instant) => TimeZoneInfo.ConvertTime(instant, timeZone).DateTime;

        var start = Local(block.Start);

        // End is exclusive: an all-day event ending at midnight means the previous day was the last day of absence.
        var lastAbsentDay = Local(block.End.AddTicks(-1));
        var end = Local(block.End);
        var returnAt = Local(block.ReturnAt);

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["StartDate"] = start.ToString("d", culture),
            ["StartDateTime"] = start.ToString("g", culture),
            ["EndDate"] = lastAbsentDay.ToString("d", culture),
            ["EndDateTime"] = end.ToString("g", culture),
            ["ReturnDate"] = returnAt.ToString("d", culture),
            ["ReturnDateTime"] = returnAt.ToString("g", culture),
        };

        return Placeholder().Replace(template, m => values.TryGetValue(m.Groups[1].Value, out var value) ? value : m.Value);
    }

    [GeneratedRegex(@"\{(\w+)\}")]
    private static partial Regex Placeholder();
}
