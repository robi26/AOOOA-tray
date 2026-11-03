namespace SST.AutoOutOfOfficeAssistant.Core.Contracts;

/// <summary>
/// The user's settings as exchanged between the web app and the API.
/// </summary>
public sealed record SettingsDto
{
    /// <summary>The maximum length of a template, in characters.</summary>
    public const int MaxTemplateLength = 16_000;

    /// <summary>Gets a value indicating whether the app manages the user's automatic replies.</summary>
    public bool Enabled { get; init; }

    /// <summary>Gets the template for senders inside the organisation.</summary>
    public string InternalTemplate { get; init; } = DefaultTemplates.Internal;

    /// <summary>Gets the template for external senders.</summary>
    public string ExternalTemplate { get; init; } = DefaultTemplates.External;

    /// <summary>Gets who outside the organisation receives a reply.</summary>
    public ExternalAudience ExternalAudience { get; init; } = ExternalAudience.All;

    /// <summary>Gets the culture used to format dates.</summary>
    public string Culture { get; init; } = SupportedCultures.Default;

    /// <summary>Creates the DTO from stored settings.</summary>
    /// <param name="settings">The settings.</param>
    /// <returns>The DTO.</returns>
    public static SettingsDto From(UserSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return new SettingsDto
        {
            Enabled = settings.Enabled,
            InternalTemplate = settings.InternalTemplate,
            ExternalTemplate = settings.ExternalTemplate,
            ExternalAudience = settings.ExternalAudience,
            Culture = settings.Culture,
        };
    }

    /// <summary>Validates the DTO.</summary>
    /// <returns>The validation errors (German, shown in the UI); empty when valid.</returns>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(this.InternalTemplate))
        {
            errors.Add("Die interne Nachricht darf nicht leer sein.");
        }
        else if (this.InternalTemplate.Length > MaxTemplateLength)
        {
            errors.Add($"Die interne Nachricht ist länger als {MaxTemplateLength} Zeichen.");
        }

        if (this.ExternalAudience != ExternalAudience.None)
        {
            if (string.IsNullOrWhiteSpace(this.ExternalTemplate))
            {
                errors.Add("Die externe Nachricht darf nicht leer sein.");
            }
            else if (this.ExternalTemplate.Length > MaxTemplateLength)
            {
                errors.Add($"Die externe Nachricht ist länger als {MaxTemplateLength} Zeichen.");
            }
        }
        else if (this.ExternalTemplate?.Length > MaxTemplateLength)
        {
            errors.Add($"Die externe Nachricht ist länger als {MaxTemplateLength} Zeichen.");
        }

        if (!Enum.IsDefined(this.ExternalAudience))
        {
            errors.Add("Ungültiger Empfängerkreis für externe Antworten.");
        }

        if (!SupportedCultures.IsSupported(this.Culture))
        {
            errors.Add("Ungültiges Datumsformat.");
        }

        return errors;
    }

    /// <summary>Creates the settings to store.</summary>
    /// <param name="userKey">The user's key (object ID).</param>
    /// <param name="userPrincipalName">The user's UPN, stored for diagnostics.</param>
    /// <returns>The settings.</returns>
    public UserSettings ToSettings(string userKey, string? userPrincipalName = null) => new()
    {
        UserKey = userKey,
        UserPrincipalName = userPrincipalName,
        Enabled = this.Enabled,
        InternalTemplate = this.InternalTemplate,
        ExternalTemplate = this.ExternalTemplate ?? string.Empty,
        ExternalAudience = this.ExternalAudience,
        Culture = SupportedCultures.All.First(c => string.Equals(c.Name, this.Culture, StringComparison.OrdinalIgnoreCase)).Name,
    };
}
