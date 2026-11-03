using System.Globalization;

namespace SST.AutoOutOfOfficeAssistant.Core.Sync;

/// <summary>
/// Builds the automatic replies setting an absence block calls for.
/// </summary>
public static class DesiredReplies
{
    /// <summary>
    /// Builds a scheduled reply from the block start until the user is back at work, with the rendered templates.
    /// </summary>
    /// <param name="block">The absence block.</param>
    /// <param name="settings">The user's settings.</param>
    /// <param name="timeZone">The mailbox time zone, used to format the dates.</param>
    /// <returns>The setting to write.</returns>
    public static AutomaticReplies Build(AbsenceBlock block, UserSettings settings, TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(block);
        ArgumentNullException.ThrowIfNull(settings);

        var culture = GetCulture(settings.Culture);
        return new AutomaticReplies(
            ReplyStatus.Scheduled,
            settings.ExternalAudience,
            block.Start,
            block.ReturnAt,
            TemplateRenderer.Render(settings.InternalTemplate, block, timeZone, culture),
            TemplateRenderer.Render(settings.ExternalTemplate, block, timeZone, culture));
    }

    private static CultureInfo GetCulture(string? name)
    {
        try
        {
            return CultureInfo.GetCultureInfo(string.IsNullOrWhiteSpace(name) ? SupportedCultures.Default : name);
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.GetCultureInfo(SupportedCultures.Default);
        }
    }
}
