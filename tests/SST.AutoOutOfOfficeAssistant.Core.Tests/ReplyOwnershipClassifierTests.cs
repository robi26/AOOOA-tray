using SST.AutoOutOfOfficeAssistant.Core;
using static SST.AutoOutOfOfficeAssistant.Core.Tests.Zurich;

namespace SST.AutoOutOfOfficeAssistant.Core.Tests;

public class ReplyOwnershipClassifierTests
{
    private static readonly DateTimeOffset Now = Nov(3, 10);

    private static readonly AutomaticReplies Ours = new(
        ReplyStatus.Scheduled, ExternalAudience.All, Nov(6), Nov(9, 8), "<p>Bis 06.11.2026</p>", "<p>Until 06.11.2026</p>");

    [Test]
    public async Task Classify_Disabled_IsFree()
    {
        var result = ReplyOwnershipClassifier.Classify(Ours with { Status = ReplyStatus.Disabled }, null, Now);

        await Assert.That(result).IsEqualTo(ReplyOwnership.Free);
    }

    [Test]
    public async Task Classify_ExpiredManualSchedule_IsFree()
    {
        var expired = Ours with { ScheduledStart = Nov(1), ScheduledEnd = Nov(2) };

        var result = ReplyOwnershipClassifier.Classify(expired, lastWrittenFingerprint: null, Now);

        await Assert.That(result).IsEqualTo(ReplyOwnership.Free);
    }

    [Test]
    public async Task Classify_MatchesLastWrite_IsOwned()
    {
        var result = ReplyOwnershipClassifier.Classify(Ours, ReplyFingerprint.Compute(Ours), Now);

        await Assert.That(result).IsEqualTo(ReplyOwnership.Owned);
    }

    [Test]
    public async Task Classify_ExchangeRewroteTheHtml_IsStillOwned()
    {
        var asStored = Ours with
        {
            InternalMessage = "<html><head><style>p{margin:0}</style></head><body><div>Bis&nbsp;06.11.2026</div></body></html>",
            ExternalMessage = "<html>\r\n<body>\r\n<p>Until  06.11.2026</p>\r\n</body>\r\n</html>",
        };

        var result = ReplyOwnershipClassifier.Classify(asStored, ReplyFingerprint.Compute(Ours), Now);

        await Assert.That(result).IsEqualTo(ReplyOwnership.Owned);
    }

    [Test]
    public async Task Classify_StaleReadBackButMatchesWhatWasSent_IsOwned()
    {
        var result = ReplyOwnershipClassifier.Classify(Ours, "STALE", Now, lastDesiredFingerprint: ReplyFingerprint.Compute(Ours));

        await Assert.That(result).IsEqualTo(ReplyOwnership.Owned);
    }

    [Test]
    public async Task Classify_AlwaysEnabledByUser_IsManual()
    {
        var manual = Ours with { Status = ReplyStatus.AlwaysEnabled };

        var result = ReplyOwnershipClassifier.Classify(manual, ReplyFingerprint.Compute(Ours), Now);

        await Assert.That(result).IsEqualTo(ReplyOwnership.Manual);
    }

    [Test]
    public async Task Classify_UserEditedTheMessage_IsManual()
    {
        var edited = Ours with { InternalMessage = "<p>Ich bin in den Ferien.</p>" };

        var result = ReplyOwnershipClassifier.Classify(edited, ReplyFingerprint.Compute(Ours), Now);

        await Assert.That(result).IsEqualTo(ReplyOwnership.Manual);
    }

    [Test]
    public async Task Classify_ActiveScheduleWithoutAnyWrite_IsManual()
    {
        var result = ReplyOwnershipClassifier.Classify(Ours, lastWrittenFingerprint: null, Now);

        await Assert.That(result).IsEqualTo(ReplyOwnership.Manual);
    }
}
