using System.Runtime.CompilerServices;
using SST.AutoOutOfOfficeAssistant.Core;
using SST.AutoOutOfOfficeAssistant.Core.Sync;

namespace SST.AutoOutOfOfficeAssistant.Core.Tests;

internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => this.Now;
}

/// <summary>An in-memory mailbox that rewrites HTML on write the way Exchange does.</summary>
internal sealed class FakeMailbox : IMailboxGateway
{
    public List<TimeRange> OofEvents { get; } = [];

    public AutomaticReplies Replies { get; set; } = AutomaticReplies.Off;

    public List<AutomaticReplies> Writes { get; } = [];

    public List<TimeRange> RequestedWindows { get; } = [];

    public Exception? FailWith { get; set; }

    public Task<MailboxSnapshot> GetMailboxAsync(string userKey, CancellationToken cancellationToken)
    {
        if (this.FailWith is not null)
        {
            throw this.FailWith;
        }

        return Task.FromResult(new MailboxSnapshot(Zurich.Zone, Zurich.OfficeHours, this.Replies));
    }

    public Task<IReadOnlyList<TimeRange>> GetOofEventsAsync(string userKey, TimeRange window, TimeZoneInfo timeZone, CancellationToken cancellationToken)
    {
        this.RequestedWindows.Add(window);
        IReadOnlyList<TimeRange> result = this.OofEvents.Where(e => e.Overlaps(window)).ToList();
        return Task.FromResult(result);
    }

    public Task<AutomaticReplies> SetAutomaticRepliesAsync(string userKey, AutomaticReplies replies, CancellationToken cancellationToken)
    {
        this.Writes.Add(replies);
        this.Replies = replies with
        {
            InternalMessage = Wrap(replies.InternalMessage),
            ExternalMessage = Wrap(replies.ExternalMessage),
        };
        return Task.FromResult(this.Replies);
    }

    private static string? Wrap(string? html) => html is null ? null : $"<html>\r\n<head></head>\r\n<body>\r\n{html}\r\n</body>\r\n</html>";
}

internal sealed class FakeUserStore : IUserStore
{
    public Dictionary<string, UserSettings> Settings { get; } = [];

    public Dictionary<string, SyncState> States { get; } = [];

    public Task<UserSettings?> GetSettingsAsync(string userKey, CancellationToken cancellationToken) =>
        Task.FromResult(this.Settings.GetValueOrDefault(userKey));

    public Task SaveSettingsAsync(UserSettings settings, CancellationToken cancellationToken)
    {
        this.Settings[settings.UserKey] = settings;
        return Task.CompletedTask;
    }

    public Task<SyncState> GetStateAsync(string userKey, CancellationToken cancellationToken) =>
        Task.FromResult(this.States.GetValueOrDefault(userKey) ?? SyncState.Empty);

    public Task SaveStateAsync(string userKey, SyncState state, CancellationToken cancellationToken)
    {
        this.States[userKey] = state;
        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<string> ListEnabledUserKeysAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var settings in this.Settings.Values.Where(s => s.Enabled))
        {
            await Task.Yield();
            yield return settings.UserKey;
        }
    }
}
