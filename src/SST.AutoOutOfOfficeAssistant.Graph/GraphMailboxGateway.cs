using System.Globalization;
using SST.AutoOutOfOfficeAssistant.Core;
using SST.AutoOutOfOfficeAssistant.Core.Sync;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Graph.Models.ODataErrors;
using GraphWorkingHours = Microsoft.Graph.Models.WorkingHours;
using TimeRange = SST.AutoOutOfOfficeAssistant.Core.TimeRange;
using WorkingHours = SST.AutoOutOfOfficeAssistant.Core.WorkingHours;

namespace SST.AutoOutOfOfficeAssistant.Graph;

/// <summary>
/// Reads calendars and writes automatic replies with Microsoft Graph. Works with application permissions
/// (background sync: <c>Calendars.Read</c>, <c>MailboxSettings.ReadWrite</c>) and with delegated permissions (tray app:
/// <c>Calendars.ReadBasic</c>, <c>MailboxSettings.ReadWrite</c>); the user is addressed by object ID either way.
/// </summary>
/// <param name="graph">The Graph client.</param>
public sealed class GraphMailboxGateway(GraphServiceClient graph) : IMailboxGateway
{
    private const string PreferUtc = "outlook.timezone=\"UTC\"";
    private const int PageSize = 200;
    private const int ReadBackAttempts = 3;

    /// <inheritdoc/>
    public async Task<MailboxSnapshot> GetMailboxAsync(string userKey, CancellationToken cancellationToken)
    {
        var settings = await Call(() => graph.Users[userKey].MailboxSettings.GetAsync(cancellationToken: cancellationToken)).ConfigureAwait(false);

        var mailboxTimeZone = TimeZoneResolver.Resolve(settings?.TimeZone, settings?.WorkingHours?.TimeZone?.Name);
        return new MailboxSnapshot(
            mailboxTimeZone,
            MapWorkingHours(settings?.WorkingHours, mailboxTimeZone),
            MapReplies(settings?.AutomaticRepliesSetting));
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TimeRange>> GetOofEventsAsync(string userKey, TimeRange window, TimeZoneInfo timeZone, CancellationToken cancellationToken)
    {
        var firstPage = await Call(() => graph.Users[userKey].CalendarView.GetAsync(
            request =>
            {
                request.QueryParameters.StartDateTime = window.Start.UtcDateTime.ToString("o", CultureInfo.InvariantCulture);
                request.QueryParameters.EndDateTime = window.End.UtcDateTime.ToString("o", CultureInfo.InvariantCulture);
                request.QueryParameters.Select = ["showAs", "start", "end", "isCancelled", "isAllDay"];
                request.QueryParameters.Top = PageSize;
                request.Headers.Add("Prefer", PreferUtc);
            },
            cancellationToken)).ConfigureAwait(false);

        var events = new List<TimeRange>();
        if (firstPage is null)
        {
            return events;
        }

        var iterator = PageIterator<Event, EventCollectionResponse>.CreatePageIterator(
            graph,
            firstPage,
            e =>
            {
                if (e.ShowAs == FreeBusyStatus.Oof && e.IsCancelled != true && ToRange(e, timeZone) is { } range)
                {
                    events.Add(range);
                }

                return true;
            },
            request =>
            {
                request.Headers.Add("Prefer", PreferUtc);
                return request;
            });

        await Call(async () =>
        {
            await iterator.IterateAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }).ConfigureAwait(false);

        return events;
    }

    /// <inheritdoc/>
    public async Task<AutomaticReplies> SetAutomaticRepliesAsync(string userKey, AutomaticReplies replies, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(replies);

        await Call(() => graph.Users[userKey].MailboxSettings.PatchAsync(
            new MailboxSettings { AutomaticRepliesSetting = ToGraph(replies) },
            cancellationToken: cancellationToken)).ConfigureAwait(false);

        // Read back what Exchange stored: it rewrites the HTML of the messages. Retry briefly in case the
        // first read still returns the previous value.
        AutomaticReplies stored = replies;
        for (var attempt = 1; attempt <= ReadBackAttempts; attempt++)
        {
            stored = (await this.GetMailboxAsync(userKey, cancellationToken).ConfigureAwait(false)).Replies;
            if (SameSchedule(stored, replies))
            {
                break;
            }

            await Task.Delay(TimeSpan.FromSeconds(attempt), cancellationToken).ConfigureAwait(false);
        }

        return stored;
    }

    private static bool SameSchedule(AutomaticReplies a, AutomaticReplies b) =>
        a.Status == b.Status
        && a.ExternalAudience == b.ExternalAudience
        && (a.Status != ReplyStatus.Scheduled
            || (SameMinute(a.ScheduledStart, b.ScheduledStart) && SameMinute(a.ScheduledEnd, b.ScheduledEnd)));

    private static bool SameMinute(DateTimeOffset? a, DateTimeOffset? b) =>
        a is null || b is null ? a == b : Math.Abs((a.Value - b.Value).TotalMinutes) < 1;

    private static async Task<T> Call<T>(Func<Task<T>> action)
    {
        try
        {
            return await action().ConfigureAwait(false);
        }
        catch (ODataError ex) when (ex.ResponseStatusCode is 403 or 404)
        {
            // 403: outside the Exchange RBAC scope; 404: no Exchange Online mailbox.
            throw new MailboxNotAccessibleException($"Graph {ex.ResponseStatusCode} {ex.Error?.Code}: {ex.Error?.Message}", ex);
        }
    }

    private static WorkingHours MapWorkingHours(GraphWorkingHours? source, TimeZoneInfo mailboxTimeZone)
    {
        if (source?.DaysOfWeek is not { Count: > 0 } days || source.StartTime is not { } start || source.EndTime is not { } end)
        {
            return WorkingHours.Default(mailboxTimeZone);
        }

        var timeZone = TimeZoneResolver.Resolve(source.TimeZone?.Name, mailboxTimeZone.Id);
        var dayOfWeeks = days
            .Where(d => d is not null)
            .Select(d => Enum.Parse<DayOfWeek>(d!.Value.ToString(), ignoreCase: true));

        return new WorkingHours(
            dayOfWeeks,
            new TimeOnly(start.Hour, start.Minute, start.Second),
            new TimeOnly(end.Hour, end.Minute, end.Second),
            timeZone);
    }

    private static AutomaticReplies MapReplies(AutomaticRepliesSetting? source)
    {
        if (source is null)
        {
            return AutomaticReplies.Off;
        }

        return new AutomaticReplies(
            source.Status switch
            {
                AutomaticRepliesStatus.AlwaysEnabled => ReplyStatus.AlwaysEnabled,
                AutomaticRepliesStatus.Scheduled => ReplyStatus.Scheduled,
                _ => ReplyStatus.Disabled,
            },
            source.ExternalAudience switch
            {
                ExternalAudienceScope.ContactsOnly => ExternalAudience.ContactsOnly,
                ExternalAudienceScope.All => ExternalAudience.All,
                _ => ExternalAudience.None,
            },
            ParseDateTime(source.ScheduledStartDateTime),
            ParseDateTime(source.ScheduledEndDateTime),
            source.InternalReplyMessage,
            source.ExternalReplyMessage);
    }

    private static AutomaticRepliesSetting ToGraph(AutomaticReplies replies)
    {
        // Only set what is known: assigning null would send an explicit null to Graph.
        var setting = new AutomaticRepliesSetting
        {
            Status = replies.Status switch
            {
                ReplyStatus.AlwaysEnabled => AutomaticRepliesStatus.AlwaysEnabled,
                ReplyStatus.Scheduled => AutomaticRepliesStatus.Scheduled,
                _ => AutomaticRepliesStatus.Disabled,
            },
            ExternalAudience = replies.ExternalAudience switch
            {
                ExternalAudience.ContactsOnly => ExternalAudienceScope.ContactsOnly,
                ExternalAudience.All => ExternalAudienceScope.All,
                _ => ExternalAudienceScope.None,
            },
        };

        if (replies.ScheduledStart is { } start)
        {
            setting.ScheduledStartDateTime = ToGraph(start);
        }

        if (replies.ScheduledEnd is { } end)
        {
            setting.ScheduledEndDateTime = ToGraph(end);
        }

        if (replies.InternalMessage is not null)
        {
            setting.InternalReplyMessage = replies.InternalMessage;
        }

        if (replies.ExternalMessage is not null)
        {
            setting.ExternalReplyMessage = replies.ExternalMessage;
        }

        return setting;
    }

    private static DateTimeTimeZone ToGraph(DateTimeOffset instant) => new()
    {
        DateTime = instant.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture),
        TimeZone = "UTC",
    };

    /// <summary>
    /// Returns the event's time range. All-day events are "floating": Graph returns them as midnight in the
    /// requested time zone (UTC here), so their dates are re-read as midnight in the mailbox time zone.
    /// </summary>
    internal static TimeRange? ToRange(Event e, TimeZoneInfo timeZone)
    {
        var start = e.IsAllDay == true ? ParseDate(e.Start, timeZone) : ParseDateTime(e.Start);
        var end = e.IsAllDay == true ? ParseDate(e.End, timeZone) : ParseDateTime(e.End);
        return start is { } s && end is { } en && en > s ? new TimeRange(s, en) : null;
    }

    /// <summary>Converts the date part of a Graph date/time to midnight in <paramref name="timeZone"/>.</summary>
    internal static DateTimeOffset? ParseDate(DateTimeTimeZone? value, TimeZoneInfo timeZone)
    {
        if (string.IsNullOrWhiteSpace(value?.DateTime)
            || !DateTime.TryParse(value.DateTime, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
        {
            return null;
        }

        var local = DateTime.SpecifyKind(parsed.Date, DateTimeKind.Unspecified);
        while (timeZone.IsInvalidTime(local))
        {
            // Midnight skipped by a DST change in some zones: the day starts at the first valid time.
            local = local.AddMinutes(30);
        }

        return new DateTimeOffset(local, timeZone.GetUtcOffset(local)).ToUniversalTime();
    }

    /// <summary>Converts a Graph date/time with time zone name to an instant.</summary>
    internal static DateTimeOffset? ParseDateTime(DateTimeTimeZone? value)
    {
        if (string.IsNullOrWhiteSpace(value?.DateTime)
            || !DateTime.TryParse(value.DateTime, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
        {
            return null;
        }

        if (parsed.Kind == DateTimeKind.Utc)
        {
            return new DateTimeOffset(parsed);
        }

        var timeZone = TimeZoneResolver.Resolve(value.TimeZone ?? "UTC");
        var local = DateTime.SpecifyKind(parsed, DateTimeKind.Unspecified);
        return new DateTimeOffset(local, timeZone.GetUtcOffset(local)).ToUniversalTime();
    }
}
