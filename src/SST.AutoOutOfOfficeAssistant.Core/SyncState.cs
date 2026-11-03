using System.Text.Json.Serialization;

namespace SST.AutoOutOfOfficeAssistant.Core;

/// <summary>Outcome of the last sync of a mailbox.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SyncResult>))]
public enum SyncResult
{
    /// <summary>The mailbox has not been synced yet.</summary>
    NotRun,

    /// <summary>No absence coming up; no reply set by the app.</summary>
    Idle,

    /// <summary>A reply is scheduled for the current or next absence.</summary>
    Scheduled,

    /// <summary>The user manages the reply themselves; the app leaves it alone.</summary>
    SkippedManual,

    /// <summary>The app may not access the mailbox (outside the Exchange RBAC scope, or no mailbox).</summary>
    NotInScope,

    /// <summary>The sync failed.</summary>
    Error,
}

/// <summary>
/// What the background sync last did for a user. Written only by the sync.
/// </summary>
public sealed record SyncState
{
    /// <summary>Gets an empty state.</summary>
    public static SyncState Empty { get; } = new();

    /// <summary>Gets the fingerprint of the setting the app last sent (before Exchange normalised it).</summary>
    public string? LastDesiredFingerprint { get; init; }

    /// <summary>Gets the fingerprint of the setting the app read back after its last write.</summary>
    public string? LastWrittenFingerprint { get; init; }

    /// <summary>Gets the start of the detected absence block.</summary>
    public DateTimeOffset? BlockStart { get; init; }

    /// <summary>Gets the end of the detected absence block.</summary>
    public DateTimeOffset? BlockEnd { get; init; }

    /// <summary>Gets the return time of the detected absence block.</summary>
    public DateTimeOffset? ReturnAt { get; init; }

    /// <summary>Gets when the sync last ran.</summary>
    public DateTimeOffset? LastRunUtc { get; init; }

    /// <summary>Gets the outcome of the last sync.</summary>
    public SyncResult LastResult { get; init; } = SyncResult.NotRun;

    /// <summary>Gets the error of the last sync, if any.</summary>
    public string? LastError { get; init; }
}
