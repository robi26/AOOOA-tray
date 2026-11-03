using System.Text.Json.Serialization;

namespace SST.AutoOutOfOfficeAssistant.Core;

/// <summary>Status of the Outlook automatic replies (Graph <c>automaticRepliesStatus</c>).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ReplyStatus>))]
public enum ReplyStatus
{
    /// <summary>Automatic replies are off.</summary>
    Disabled,

    /// <summary>Automatic replies are on until switched off.</summary>
    AlwaysEnabled,

    /// <summary>Automatic replies are on between the scheduled start and end.</summary>
    Scheduled,
}

/// <summary>Who outside the organisation receives the external reply (Graph <c>externalAudienceScope</c>).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ExternalAudience>))]
public enum ExternalAudience
{
    /// <summary>No external replies.</summary>
    None,

    /// <summary>Only senders in the user's contacts.</summary>
    ContactsOnly,

    /// <summary>All external senders.</summary>
    All,
}

/// <summary>
/// The automatic replies setting of a mailbox.
/// </summary>
/// <param name="Status">The status.</param>
/// <param name="ExternalAudience">The external audience.</param>
/// <param name="ScheduledStart">Scheduled start, relevant for <see cref="ReplyStatus.Scheduled"/>.</param>
/// <param name="ScheduledEnd">Scheduled end, relevant for <see cref="ReplyStatus.Scheduled"/>.</param>
/// <param name="InternalMessage">The reply sent to senders inside the organisation (HTML).</param>
/// <param name="ExternalMessage">The reply sent to external senders (HTML).</param>
public sealed record AutomaticReplies(
    ReplyStatus Status,
    ExternalAudience ExternalAudience,
    DateTimeOffset? ScheduledStart,
    DateTimeOffset? ScheduledEnd,
    string? InternalMessage,
    string? ExternalMessage)
{
    /// <summary>Gets a disabled setting without messages.</summary>
    public static AutomaticReplies Off { get; } = new(ReplyStatus.Disabled, ExternalAudience.None, null, null, null, null);
}
