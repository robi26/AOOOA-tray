namespace SST.AutoOutOfOfficeAssistant.Core.Sync;

/// <summary>What the sync needs to know about a mailbox.</summary>
/// <param name="TimeZone">The mailbox time zone.</param>
/// <param name="WorkingHours">The working hours (or the default when none are configured).</param>
/// <param name="Replies">The current automatic replies setting.</param>
public sealed record MailboxSnapshot(TimeZoneInfo TimeZone, WorkingHours WorkingHours, AutomaticReplies Replies);

/// <summary>Access to a user's mailbox (implemented with Microsoft Graph).</summary>
public interface IMailboxGateway
{
    /// <summary>Reads time zone, working hours and automatic replies.</summary>
    /// <param name="userKey">The user (UPN).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The snapshot.</returns>
    /// <exception cref="MailboxNotAccessibleException">The app may not access the mailbox.</exception>
    Task<MailboxSnapshot> GetMailboxAsync(string userKey, CancellationToken cancellationToken);

    /// <summary>Returns the non-cancelled events shown as "Out of office" that overlap <paramref name="window"/>.</summary>
    /// <param name="userKey">The user (UPN).</param>
    /// <param name="window">The window.</param>
    /// <param name="timeZone">The mailbox time zone: all-day events run from midnight to midnight in it.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The events.</returns>
    Task<IReadOnlyList<TimeRange>> GetOofEventsAsync(string userKey, TimeRange window, TimeZoneInfo timeZone, CancellationToken cancellationToken);

    /// <summary>Writes the automatic replies and returns the setting as Exchange stored it.</summary>
    /// <param name="userKey">The user (UPN).</param>
    /// <param name="replies">The setting to write.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The setting read back after the write.</returns>
    Task<AutomaticReplies> SetAutomaticRepliesAsync(string userKey, AutomaticReplies replies, CancellationToken cancellationToken);
}

/// <summary>Storage for user settings and sync state.</summary>
public interface IUserStore
{
    /// <summary>Reads the settings, or null when the user never saved any.</summary>
    /// <param name="userKey">The user.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The settings or null.</returns>
    Task<UserSettings?> GetSettingsAsync(string userKey, CancellationToken cancellationToken);

    /// <summary>Saves the settings.</summary>
    /// <param name="settings">The settings.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    Task SaveSettingsAsync(UserSettings settings, CancellationToken cancellationToken);

    /// <summary>Reads the sync state, or <see cref="SyncState.Empty"/>.</summary>
    /// <param name="userKey">The user.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The state.</returns>
    Task<SyncState> GetStateAsync(string userKey, CancellationToken cancellationToken);

    /// <summary>Saves the sync state.</summary>
    /// <param name="userKey">The user.</param>
    /// <param name="state">The state.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    Task SaveStateAsync(string userKey, SyncState state, CancellationToken cancellationToken);

    /// <summary>Lists the keys of all users who opted in.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The keys.</returns>
    IAsyncEnumerable<string> ListEnabledUserKeysAsync(CancellationToken cancellationToken);
}

/// <summary>Queue of users whose mailbox should be synced.</summary>
public interface ISyncQueue
{
    /// <summary>The name of the queue.</summary>
    public const string QueueName = "sync-requests";

    /// <summary>Requests a sync of the user's mailbox.</summary>
    /// <param name="userKey">The user.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    Task EnqueueAsync(string userKey, CancellationToken cancellationToken);
}

/// <summary>
/// The app may not access the mailbox: the user is outside the Exchange RBAC scope or has no Exchange Online mailbox.
/// </summary>
public sealed class MailboxNotAccessibleException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="MailboxNotAccessibleException"/> class.</summary>
    public MailboxNotAccessibleException()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="MailboxNotAccessibleException"/> class.</summary>
    /// <param name="message">The message.</param>
    public MailboxNotAccessibleException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="MailboxNotAccessibleException"/> class.</summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The cause.</param>
    public MailboxNotAccessibleException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
