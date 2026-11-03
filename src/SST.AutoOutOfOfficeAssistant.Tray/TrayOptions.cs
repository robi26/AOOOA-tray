using System.Text.Json;

namespace SST.AutoOutOfOfficeAssistant.Tray;

/// <summary>
/// Settings from <c>appsettings.json</c> next to the exe.
/// </summary>
internal sealed class TrayOptions
{
    /// <summary>Gets the client ID of the app-ext-aoooatray-p app registration (multi-tenant public client).</summary>
    public string ClientId { get; init; } = string.Empty;

    /// <summary>Gets the authority. <c>organizations</c>: work or school accounts of any tenant.</summary>
    public string Authority { get; init; } = "https://login.microsoftonline.com/organizations";

    /// <summary>Gets the minutes between two calendar checks.</summary>
    public int SyncIntervalMinutes { get; init; } = 15;

    /// <summary>Gets a value indicating whether a real client ID is configured.</summary>
    public bool IsConfigured => Guid.TryParse(this.ClientId, out var id) && id != Guid.Empty;

    /// <summary>Gets the check interval, at least 5 minutes.</summary>
    public TimeSpan SyncInterval => TimeSpan.FromMinutes(Math.Max(5, this.SyncIntervalMinutes));

    /// <summary>Loads the options, or returns the defaults when the file is missing or invalid.</summary>
    /// <param name="path">The file path.</param>
    /// <returns>The options.</returns>
    public static TrayOptions Load(string path)
    {
        try
        {
            return File.Exists(path)
                ? JsonSerializer.Deserialize<TrayOptions>(
                    File.ReadAllText(path),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip }) ?? new()
                : new();
        }
        catch (JsonException ex)
        {
            FileLog.Error("Invalid appsettings.json", ex);
            return new();
        }
    }
}
