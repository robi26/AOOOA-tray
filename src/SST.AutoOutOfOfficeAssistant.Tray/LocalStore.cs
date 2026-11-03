using System.Runtime.CompilerServices;
using System.Text.Json;
using SST.AutoOutOfOfficeAssistant.Core;
using SST.AutoOutOfOfficeAssistant.Core.Sync;

namespace SST.AutoOutOfOfficeAssistant.Tray;

/// <summary>Everything the tray app remembers for the Windows user.</summary>
internal sealed class TrayData
{
    /// <summary>Gets or sets the MSAL account identifier of the signed-in user.</summary>
    public string? AccountId { get; set; }

    /// <summary>Gets or sets the signed-in user's object ID.</summary>
    public string? UserKey { get; set; }

    /// <summary>Gets or sets a value indicating whether the app manages the automatic replies.</summary>
    public bool Enabled { get; set; }

    /// <summary>Gets or sets the internal reply template (plain text).</summary>
    public string InternalText { get; set; } = PlainTextTemplate.Default;

    /// <summary>Gets or sets the external reply template (plain text).</summary>
    public string ExternalText { get; set; } = PlainTextTemplate.Default;

    /// <summary>Gets or sets who outside the organisation receives a reply.</summary>
    public ExternalAudience ExternalAudience { get; set; } = ExternalAudience.All;

    /// <summary>Gets or sets the culture for dates in the reply.</summary>
    public string Culture { get; set; } = SupportedCultures.Default;

    /// <summary>Gets or sets the state of the last sync.</summary>
    public SyncState State { get; set; } = SyncState.Empty;
}

/// <summary>
/// The <see cref="IUserStore"/> of the tray app: one user, kept in <c>%LOCALAPPDATA%\SST\AOOOA\settings.json</c>.
/// The plain-text templates are converted to HTML for the sync.
/// </summary>
internal sealed class LocalStore : IUserStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string path;
    private readonly object gate = new();

    public LocalStore(string path)
    {
        this.path = path;
        this.Data = Load(path);
    }

    /// <summary>Gets the current data. Change it only through <see cref="Update"/>.</summary>
    public TrayData Data { get; }

    /// <summary>Changes the data and saves it.</summary>
    /// <param name="change">The change.</param>
    public void Update(Action<TrayData> change)
    {
        ArgumentNullException.ThrowIfNull(change);
        lock (this.gate)
        {
            change(this.Data);
            this.Save();
        }
    }

    /// <inheritdoc/>
    public Task<UserSettings?> GetSettingsAsync(string userKey, CancellationToken cancellationToken)
    {
        lock (this.gate)
        {
            UserSettings? settings = userKey == this.Data.UserKey
                ? new UserSettings
                {
                    UserKey = userKey,
                    Enabled = this.Data.Enabled,
                    InternalTemplate = PlainTextTemplate.ToHtml(this.Data.InternalText),
                    ExternalTemplate = PlainTextTemplate.ToHtml(this.Data.ExternalText),
                    ExternalAudience = this.Data.ExternalAudience,
                    Culture = this.Data.Culture,
                }
                : null;
            return Task.FromResult(settings);
        }
    }

    /// <inheritdoc/>
    public Task SaveSettingsAsync(UserSettings settings, CancellationToken cancellationToken) =>
        throw new NotSupportedException("The tray app edits plain-text settings through Update().");

    /// <inheritdoc/>
    public Task<SyncState> GetStateAsync(string userKey, CancellationToken cancellationToken)
    {
        lock (this.gate)
        {
            return Task.FromResult(userKey == this.Data.UserKey ? this.Data.State : SyncState.Empty);
        }
    }

    /// <inheritdoc/>
    public Task SaveStateAsync(string userKey, SyncState state, CancellationToken cancellationToken)
    {
        this.Update(d =>
        {
            if (userKey == d.UserKey)
            {
                d.State = state;
            }
        });
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<string> ListEnabledUserKeysAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        if (this.Data is { Enabled: true, UserKey: { } key })
        {
            yield return key;
        }
    }

    private static TrayData Load(string path)
    {
        try
        {
            return File.Exists(path) ? JsonSerializer.Deserialize<TrayData>(File.ReadAllText(path), JsonOptions) ?? new() : new();
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            FileLog.Error("Could not read settings; starting with defaults", ex);
            return new();
        }
    }

    private void Save()
    {
        var temp = this.path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(this.Data, JsonOptions));
        File.Move(temp, this.path, overwrite: true);
    }
}
