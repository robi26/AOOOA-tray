namespace SST.AutoOutOfOfficeAssistant.Core.Contracts;

/// <summary>
/// What the background sync last did for the user, as shown in the web app.
/// </summary>
public sealed record StatusDto
{
    /// <summary>Gets the user's key (Entra object ID).</summary>
    public required string UserKey { get; init; }

    /// <summary>Gets a value indicating whether the user has opted in.</summary>
    public bool Enabled { get; init; }

    /// <summary>Gets the outcome of the last sync.</summary>
    public SyncResult LastResult { get; init; }

    /// <summary>Gets when the sync last ran.</summary>
    public DateTimeOffset? LastRunUtc { get; init; }

    /// <summary>Gets the error of the last sync, if any.</summary>
    public string? LastError { get; init; }

    /// <summary>Gets the start of the detected absence block.</summary>
    public DateTimeOffset? BlockStart { get; init; }

    /// <summary>Gets the end of the detected absence block.</summary>
    public DateTimeOffset? BlockEnd { get; init; }

    /// <summary>Gets when the user is back at work.</summary>
    public DateTimeOffset? ReturnAt { get; init; }

    /// <summary>Creates the DTO.</summary>
    /// <param name="userKey">The user's key.</param>
    /// <param name="enabled">Whether the user opted in.</param>
    /// <param name="state">The sync state.</param>
    /// <returns>The DTO.</returns>
    public static StatusDto From(string userKey, bool enabled, SyncState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return new StatusDto
        {
            UserKey = userKey,
            Enabled = enabled,
            LastResult = state.LastResult,
            LastRunUtc = state.LastRunUtc,
            LastError = state.LastError,
            BlockStart = state.BlockStart,
            BlockEnd = state.BlockEnd,
            ReturnAt = state.ReturnAt,
        };
    }
}
