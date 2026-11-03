namespace SST.AutoOutOfOfficeAssistant.Core;

/// <summary>Who controls the current automatic replies of a mailbox.</summary>
public enum ReplyOwnership
{
    /// <summary>No active reply: the app may set one.</summary>
    Free,

    /// <summary>The active reply is exactly the one the app wrote last: the app may change or clear it.</summary>
    Owned,

    /// <summary>The user set or changed the reply themselves: the app must not touch it.</summary>
    Manual,
}

/// <summary>
/// Decides who controls the current automatic replies.
/// </summary>
public static class ReplyOwnershipClassifier
{
    /// <summary>Classifies the current setting.</summary>
    /// <param name="current">The setting currently stored in the mailbox.</param>
    /// <param name="lastWrittenFingerprint">The fingerprint of the setting the app read back after its last write, or null.</param>
    /// <param name="now">The current instant.</param>
    /// <param name="lastDesiredFingerprint">
    /// The fingerprint of the setting the app last sent, or null. Also accepted as "ours", in case the read-back
    /// after the write returned a stale value.
    /// </param>
    /// <returns>The ownership.</returns>
    public static ReplyOwnership Classify(AutomaticReplies current, string? lastWrittenFingerprint, DateTimeOffset now, string? lastDesiredFingerprint = null)
    {
        ArgumentNullException.ThrowIfNull(current);

        if (current.Status == ReplyStatus.Disabled)
        {
            return ReplyOwnership.Free;
        }

        if (current.Status == ReplyStatus.Scheduled && current.ScheduledEnd is { } end && end <= now)
        {
            return ReplyOwnership.Free;
        }

        if (lastWrittenFingerprint is not null || lastDesiredFingerprint is not null)
        {
            var fingerprint = ReplyFingerprint.Compute(current);
            if (fingerprint == lastWrittenFingerprint || fingerprint == lastDesiredFingerprint)
            {
                return ReplyOwnership.Owned;
            }
        }

        return ReplyOwnership.Manual;
    }
}
