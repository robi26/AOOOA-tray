namespace SST.AutoOutOfOfficeAssistant.Core.Sync;

/// <summary>
/// Brings one user's automatic replies in line with their calendar.
/// </summary>
/// <param name="gateway">Mailbox access.</param>
/// <param name="store">Settings and state storage.</param>
/// <param name="timeProvider">The clock.</param>
public sealed class MailboxSyncService(IMailboxGateway gateway, IUserStore store, TimeProvider timeProvider)
{
    /// <summary>Days before now that are searched, so an absence that started earlier keeps its real start date.</summary>
    public const int LookbackDays = 31;

    /// <summary>Days after now searched per calendar request.</summary>
    public const int LookaheadDays = 90;

    /// <summary>The furthest ahead the search for the end of a long absence goes.</summary>
    public const int MaxLookaheadDays = 366;

    /// <summary>
    /// Syncs the user's mailbox and stores the outcome.
    /// </summary>
    /// <param name="userKey">The user.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The new state.</returns>
    /// <remarks>
    /// Transient failures (throttling, outages) are rethrown after the error is recorded, so the queue retries the message.
    /// A mailbox the app may not access is recorded as <see cref="SyncResult.NotInScope"/> and not retried.
    /// </remarks>
    public async Task<SyncState> SyncAsync(string userKey, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userKey);

        var settings = await store.GetSettingsAsync(userKey, cancellationToken).ConfigureAwait(false);
        if (settings is null)
        {
            // Unknown user: nothing was ever written to their mailbox.
            return SyncState.Empty;
        }

        var state = await store.GetStateAsync(userKey, cancellationToken).ConfigureAwait(false);
        var enabled = settings.Enabled;

        // Opted out and nothing of ours left in the mailbox: no Graph calls needed.
        if (!enabled && state.LastWrittenFingerprint is null && state.LastDesiredFingerprint is null)
        {
            var idle = state with { LastResult = SyncResult.Idle, LastError = null, BlockStart = null, BlockEnd = null, ReturnAt = null };
            await store.SaveStateAsync(userKey, idle, cancellationToken).ConfigureAwait(false);
            return idle;
        }

        var now = timeProvider.GetUtcNow();
        SyncState next;
        try
        {
            next = await this.SyncMailboxAsync(userKey, enabled ? settings : null, state, now, cancellationToken).ConfigureAwait(false);
        }
        catch (MailboxNotAccessibleException ex)
        {
            next = state with { LastRunUtc = now, LastResult = SyncResult.NotInScope, LastError = ex.Message };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var failed = state with { LastRunUtc = now, LastResult = SyncResult.Error, LastError = ex.Message };
            await store.SaveStateAsync(userKey, failed, cancellationToken).ConfigureAwait(false);
            throw;
        }

        await store.SaveStateAsync(userKey, next, cancellationToken).ConfigureAwait(false);
        return next;
    }

    private async Task<SyncState> SyncMailboxAsync(string userKey, UserSettings? activeSettings, SyncState state, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var mailbox = await gateway.GetMailboxAsync(userKey, cancellationToken).ConfigureAwait(false);

        var block = activeSettings is null
            ? null
            : await this.FindBlockAsync(userKey, mailbox, now, cancellationToken).ConfigureAwait(false);

        var ownership = ReplyOwnershipClassifier.Classify(mailbox.Replies, state.LastWrittenFingerprint, now, state.LastDesiredFingerprint);
        var desired = block is null ? null : DesiredReplies.Build(block, activeSettings!, mailbox.TimeZone);
        var decision = SyncPlanner.Decide(ownership, desired, state.LastDesiredFingerprint);

        var observed = state with
        {
            LastRunUtc = now,
            LastError = null,
            BlockStart = block?.Start,
            BlockEnd = block?.End,
            ReturnAt = block?.ReturnAt,
        };

        switch (decision)
        {
            case SyncDecision.SkipManual:
                // The user took over; our last write can never match again. Forget it so a later
                // "Free" mailbox is treated as a fresh start.
                return observed with { LastResult = SyncResult.SkippedManual, LastDesiredFingerprint = null, LastWrittenFingerprint = null };

            case SyncDecision.Apply apply:
                var written = await gateway.SetAutomaticRepliesAsync(userKey, apply.Desired, cancellationToken).ConfigureAwait(false);
                return observed with
                {
                    LastResult = SyncResult.Scheduled,
                    LastDesiredFingerprint = ReplyFingerprint.Compute(apply.Desired),
                    LastWrittenFingerprint = ReplyFingerprint.Compute(written),
                };

            case SyncDecision.Clear:
                await gateway.SetAutomaticRepliesAsync(userKey, mailbox.Replies with { Status = ReplyStatus.Disabled }, cancellationToken).ConfigureAwait(false);
                return observed with { LastResult = SyncResult.Idle, LastDesiredFingerprint = null, LastWrittenFingerprint = null };

            default:
                if (desired is not null)
                {
                    return observed with { LastResult = SyncResult.Scheduled };
                }

                // Nothing to do and nothing of ours active (an own schedule may have expired): forget it.
                return observed with { LastResult = SyncResult.Idle, LastDesiredFingerprint = null, LastWrittenFingerprint = null };
        }
    }

    private async Task<AbsenceBlock?> FindBlockAsync(string userKey, MailboxSnapshot mailbox, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var workingHours = mailbox.WorkingHours;
        var from = now.AddDays(-LookbackDays);
        var to = now.AddDays(LookaheadDays);
        var limit = now.AddDays(MaxLookaheadDays);

        var events = new List<TimeRange>(await gateway.GetOofEventsAsync(userKey, new TimeRange(from, to), mailbox.TimeZone, cancellationToken).ConfigureAwait(false));
        var block = AbsenceBlockCalculator.Calculate(events, workingHours, now);

        // A block reaching the end of the window may continue beyond it: fetch further until it ends.
        while (block is not null && block.End >= to.AddDays(-1) && to < limit)
        {
            var next = to.AddDays(LookaheadDays);
            events.AddRange(await gateway.GetOofEventsAsync(userKey, new TimeRange(to, next), mailbox.TimeZone, cancellationToken).ConfigureAwait(false));
            to = next;
            block = AbsenceBlockCalculator.Calculate(events, workingHours, now);
        }

        return block;
    }
}
