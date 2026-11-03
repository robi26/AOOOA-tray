using SST.AutoOutOfOfficeAssistant.Core;
using SST.AutoOutOfOfficeAssistant.Core.Sync;
using static SST.AutoOutOfOfficeAssistant.Core.Tests.Zurich;

namespace SST.AutoOutOfOfficeAssistant.Core.Tests;

public class MailboxSyncServiceTests
{
    private const string User = "5d8c1a3e-2f4b-4c6d-9e7f-0a1b2c3d4e5f";

    private FakeMailbox mailbox = null!;
    private FakeUserStore store = null!;
    private FixedTimeProvider clock = null!;
    private MailboxSyncService service = null!;

    [Before(Test)]
    public void Setup()
    {
        this.mailbox = new FakeMailbox();
        this.store = new FakeUserStore();
        this.clock = new FixedTimeProvider(Nov(3, 10));
        this.service = new MailboxSyncService(this.mailbox, this.store, this.clock);
        this.store.Settings[User] = new UserSettings
        {
            UserKey = User,
            Enabled = true,
            InternalTemplate = "<p>Zurück am {ReturnDate}</p>",
            ExternalTemplate = "<p>Back on {ReturnDate}</p>",
        };
    }

    [Test]
    public async Task SyncAsync_UpcomingAbsence_SchedulesReplyUntilReturn()
    {
        this.mailbox.OofEvents.Add(AllDayNov(6));

        var state = await this.service.SyncAsync(User, CancellationToken.None);

        await Assert.That(this.mailbox.Writes).Count().IsEqualTo(1);
        var written = this.mailbox.Writes[0];
        await Assert.That(written.Status).IsEqualTo(ReplyStatus.Scheduled);
        await Assert.That(written.ScheduledStart).IsEqualTo(Nov(6));
        await Assert.That(written.ScheduledEnd).IsEqualTo(Nov(9, 8));
        await Assert.That(written.InternalMessage).IsEqualTo("<p>Zurück am 09.11.2026</p>");
        await Assert.That(state.LastResult).IsEqualTo(SyncResult.Scheduled);
        await Assert.That(state.ReturnAt).IsEqualTo(Nov(9, 8));
    }

    [Test]
    public async Task SyncAsync_SecondRunWithoutChanges_DoesNotWriteAgain()
    {
        this.mailbox.OofEvents.Add(AllDayNov(6));
        await this.service.SyncAsync(User, CancellationToken.None);

        this.clock.Now = Nov(3, 10, 15);
        var state = await this.service.SyncAsync(User, CancellationToken.None);

        await Assert.That(this.mailbox.Writes).Count().IsEqualTo(1);
        await Assert.That(state.LastResult).IsEqualTo(SyncResult.Scheduled);
    }

    [Test]
    public async Task SyncAsync_AbsenceExtended_UpdatesSchedule()
    {
        this.mailbox.OofEvents.Add(AllDayNov(6));
        await this.service.SyncAsync(User, CancellationToken.None);

        this.mailbox.OofEvents.Add(AllDayNov(9));
        await this.service.SyncAsync(User, CancellationToken.None);

        await Assert.That(this.mailbox.Writes).Count().IsEqualTo(2);
        await Assert.That(this.mailbox.Writes[1].ScheduledEnd).IsEqualTo(Nov(10, 8));
    }

    [Test]
    public async Task SyncAsync_EventDeleted_DisablesOwnReply()
    {
        this.mailbox.OofEvents.Add(AllDayNov(6));
        await this.service.SyncAsync(User, CancellationToken.None);

        this.mailbox.OofEvents.Clear();
        var state = await this.service.SyncAsync(User, CancellationToken.None);

        await Assert.That(this.mailbox.Replies.Status).IsEqualTo(ReplyStatus.Disabled);
        await Assert.That(state.LastResult).IsEqualTo(SyncResult.Idle);
        await Assert.That(state.LastWrittenFingerprint).IsNull();
    }

    [Test]
    public async Task SyncAsync_ManualReplyActive_LeavesItAlone()
    {
        var manual = new AutomaticReplies(ReplyStatus.AlwaysEnabled, ExternalAudience.All, null, null, "Ferien", "Holidays");
        this.mailbox.Replies = manual;
        this.mailbox.OofEvents.Add(AllDayNov(6));

        var state = await this.service.SyncAsync(User, CancellationToken.None);

        await Assert.That(this.mailbox.Writes).IsEmpty();
        await Assert.That(this.mailbox.Replies).IsEqualTo(manual);
        await Assert.That(state.LastResult).IsEqualTo(SyncResult.SkippedManual);
    }

    [Test]
    public async Task SyncAsync_UserEditedOurReply_StopsManagingIt()
    {
        this.mailbox.OofEvents.Add(AllDayNov(6));
        await this.service.SyncAsync(User, CancellationToken.None);
        this.mailbox.Replies = this.mailbox.Replies with { InternalMessage = "Eigener Text" };

        this.mailbox.OofEvents.Add(AllDayNov(9));
        var state = await this.service.SyncAsync(User, CancellationToken.None);

        await Assert.That(this.mailbox.Writes).Count().IsEqualTo(1);
        await Assert.That(this.mailbox.Replies.InternalMessage).IsEqualTo("Eigener Text");
        await Assert.That(state.LastResult).IsEqualTo(SyncResult.SkippedManual);
    }

    [Test]
    public async Task SyncAsync_UserOptsOut_DisablesOwnReply()
    {
        this.mailbox.OofEvents.Add(AllDayNov(6));
        await this.service.SyncAsync(User, CancellationToken.None);

        this.store.Settings[User] = this.store.Settings[User] with { Enabled = false };
        var state = await this.service.SyncAsync(User, CancellationToken.None);

        await Assert.That(this.mailbox.Replies.Status).IsEqualTo(ReplyStatus.Disabled);
        await Assert.That(state.LastResult).IsEqualTo(SyncResult.Idle);
    }

    [Test]
    public async Task SyncAsync_OptedOutWithNothingWritten_DoesNotCallGraph()
    {
        this.store.Settings[User] = this.store.Settings[User] with { Enabled = false };
        this.mailbox.FailWith = new InvalidOperationException("Graph must not be called");

        var state = await this.service.SyncAsync(User, CancellationToken.None);

        await Assert.That(state.LastResult).IsEqualTo(SyncResult.Idle);
    }

    [Test]
    public async Task SyncAsync_UnknownUser_DoesNothing()
    {
        var state = await this.service.SyncAsync("00000000-0000-4000-8000-000000000001", CancellationToken.None);

        await Assert.That(state).IsEqualTo(SyncState.Empty);
        await Assert.That(this.store.States).IsEmpty();
    }

    [Test]
    public async Task SyncAsync_MailboxNotAccessible_RecordsNotInScope()
    {
        this.mailbox.FailWith = new MailboxNotAccessibleException("Access denied");

        var state = await this.service.SyncAsync(User, CancellationToken.None);

        await Assert.That(state.LastResult).IsEqualTo(SyncResult.NotInScope);
        await Assert.That(this.store.States[User].LastError).IsEqualTo("Access denied");
    }

    [Test]
    public async Task SyncAsync_TransientFailure_RecordsErrorAndRethrows()
    {
        this.mailbox.FailWith = new HttpRequestException("Too many requests");

        await Assert.That(async () => await this.service.SyncAsync(User, CancellationToken.None))
            .ThrowsExactly<HttpRequestException>();
        await Assert.That(this.store.States[User].LastResult).IsEqualTo(SyncResult.Error);
    }

    [Test]
    public async Task SyncAsync_AbsenceBeyondFirstWindow_FetchesFurtherUntilItEnds()
    {
        // Out of office every day from now for 120 days: longer than one calendar request window.
        var start = Nov(3);
        for (var i = 0; i < 120; i++)
        {
            this.mailbox.OofEvents.Add(new TimeRange(start.AddDays(i), start.AddDays(i + 1)));
        }

        var state = await this.service.SyncAsync(User, CancellationToken.None);

        await Assert.That(this.mailbox.RequestedWindows.Count).IsGreaterThan(1);
        await Assert.That(state.BlockEnd).IsEqualTo(start.AddDays(120));
    }

    [Test]
    public async Task SyncAsync_OwnScheduleExpired_NextAbsenceIsScheduledAgain()
    {
        this.mailbox.OofEvents.Add(AllDayNov(4));
        await this.service.SyncAsync(User, CancellationToken.None);

        this.clock.Now = Nov(6, 10);
        this.mailbox.OofEvents.Add(AllDayNov(10));
        var state = await this.service.SyncAsync(User, CancellationToken.None);

        await Assert.That(this.mailbox.Writes).Count().IsEqualTo(2);
        await Assert.That(this.mailbox.Writes[1].ScheduledStart).IsEqualTo(Nov(10));
        await Assert.That(state.LastResult).IsEqualTo(SyncResult.Scheduled);
    }
}
