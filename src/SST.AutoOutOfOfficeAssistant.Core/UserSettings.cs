namespace SST.AutoOutOfOfficeAssistant.Core;

/// <summary>
/// The settings a user chose in the web app.
/// </summary>
public sealed record UserSettings
{
    /// <summary>Gets the user's key: the Entra object ID (see <see cref="UserKey"/>). Also used to address the mailbox in Graph.</summary>
    public required string UserKey { get; init; }

    /// <summary>Gets the UPN at the time of the last save. For administrators and diagnostics only; never used as an identity.</summary>
    public string? UserPrincipalName { get; init; }

    /// <summary>Gets a value indicating whether the app manages the user's automatic replies.</summary>
    public bool Enabled { get; init; }

    /// <summary>Gets the template for senders inside the organisation.</summary>
    public string InternalTemplate { get; init; } = DefaultTemplates.Internal;

    /// <summary>Gets the template for external senders.</summary>
    public string ExternalTemplate { get; init; } = DefaultTemplates.External;

    /// <summary>Gets who outside the organisation receives a reply.</summary>
    public ExternalAudience ExternalAudience { get; init; } = ExternalAudience.All;

    /// <summary>Gets the culture used to format dates in the reply.</summary>
    public string Culture { get; init; } = SupportedCultures.Default;
}

/// <summary>Cultures the reply dates can be formatted in.</summary>
public static class SupportedCultures
{
    /// <summary>The default culture.</summary>
    public const string Default = "de-CH";

    /// <summary>Gets all supported cultures with a display name.</summary>
    public static IReadOnlyList<(string Name, string DisplayName)> All { get; } =
    [
        ("de-CH", "Deutsch (Schweiz) – 24.12.2026"),
        ("fr-CH", "Français (Suisse) – 24.12.2026"),
        ("en-GB", "English (UK) – 24/12/2026"),
    ];

    /// <summary>Returns true when <paramref name="culture"/> is supported.</summary>
    /// <param name="culture">The culture name.</param>
    /// <returns>True when supported.</returns>
    public static bool IsSupported(string? culture) => All.Any(c => string.Equals(c.Name, culture, StringComparison.OrdinalIgnoreCase));
}
