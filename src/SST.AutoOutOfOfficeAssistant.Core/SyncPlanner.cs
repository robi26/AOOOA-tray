namespace SST.AutoOutOfOfficeAssistant.Core;

/// <summary>What the sync should do with a mailbox.</summary>
public abstract record SyncDecision
{
    private SyncDecision()
    {
    }

    /// <summary>Nothing to change.</summary>
    public sealed record NoChange : SyncDecision;

    /// <summary>Write <paramref name="Desired"/> to the mailbox.</summary>
    /// <param name="Desired">The setting to write.</param>
    public sealed record Apply(AutomaticReplies Desired) : SyncDecision;

    /// <summary>Switch off the reply the app wrote earlier.</summary>
    public sealed record Clear : SyncDecision;

    /// <summary>The user manages the reply themselves; leave it alone.</summary>
    public sealed record SkipManual : SyncDecision;
}

/// <summary>
/// Decides how to bring a mailbox's automatic replies in line with the calendar.
/// </summary>
public static class SyncPlanner
{
    /// <summary>Makes the decision.</summary>
    /// <param name="ownership">Who controls the current reply.</param>
    /// <param name="desired">The reply the calendar calls for, or null when no absence is coming up.</param>
    /// <param name="lastDesiredFingerprint">Fingerprint of the setting the app last sent, or null.</param>
    /// <returns>The decision.</returns>
    public static SyncDecision Decide(ReplyOwnership ownership, AutomaticReplies? desired, string? lastDesiredFingerprint)
    {
        if (ownership == ReplyOwnership.Manual)
        {
            return new SyncDecision.SkipManual();
        }

        if (desired is not null)
        {
            var unchanged = ownership == ReplyOwnership.Owned
                && ReplyFingerprint.Compute(desired) == lastDesiredFingerprint;
            return unchanged ? new SyncDecision.NoChange() : new SyncDecision.Apply(desired);
        }

        // No absence: switch off a reply the app still owns (for example after the event was deleted).
        return ownership == ReplyOwnership.Owned ? new SyncDecision.Clear() : new SyncDecision.NoChange();
    }
}
