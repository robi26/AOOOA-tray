using SST.AutoOutOfOfficeAssistant.Core;
using static SST.AutoOutOfOfficeAssistant.Core.Tests.Zurich;

namespace SST.AutoOutOfOfficeAssistant.Core.Tests;

public class SyncPlannerTests
{
    private static readonly AutomaticReplies Desired = new(
        ReplyStatus.Scheduled, ExternalAudience.All, Nov(6), Nov(9, 8), "intern", "extern");

    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task Decide_Manual_SkipsWhetherOrNotAnAbsenceIsComing(bool hasAbsence)
    {
        var decision = SyncPlanner.Decide(ReplyOwnership.Manual, hasAbsence ? Desired : null, null);

        await Assert.That(decision).IsTypeOf<SyncDecision.SkipManual>();
    }

    [Test]
    public async Task Decide_FreeWithAbsence_Applies()
    {
        var decision = SyncPlanner.Decide(ReplyOwnership.Free, Desired, null);

        await Assert.That(decision).IsEqualTo(new SyncDecision.Apply(Desired));
    }

    [Test]
    public async Task Decide_FreeWithAbsenceAndSameFingerprint_StillApplies()
    {
        // Free means nothing is active (for example the user switched the reply off): set it again.
        var decision = SyncPlanner.Decide(ReplyOwnership.Free, Desired, ReplyFingerprint.Compute(Desired));

        await Assert.That(decision).IsTypeOf<SyncDecision.Apply>();
    }

    [Test]
    public async Task Decide_OwnedAndUnchanged_DoesNothing()
    {
        var decision = SyncPlanner.Decide(ReplyOwnership.Owned, Desired, ReplyFingerprint.Compute(Desired));

        await Assert.That(decision).IsTypeOf<SyncDecision.NoChange>();
    }

    [Test]
    public async Task Decide_OwnedAndAbsenceExtended_Applies()
    {
        var previous = Desired with { ScheduledEnd = Nov(6, 17) };

        var decision = SyncPlanner.Decide(ReplyOwnership.Owned, Desired, ReplyFingerprint.Compute(previous));

        await Assert.That(decision).IsEqualTo(new SyncDecision.Apply(Desired));
    }

    [Test]
    public async Task Decide_OwnedWithoutAbsence_Clears()
    {
        var decision = SyncPlanner.Decide(ReplyOwnership.Owned, null, ReplyFingerprint.Compute(Desired));

        await Assert.That(decision).IsTypeOf<SyncDecision.Clear>();
    }

    [Test]
    public async Task Decide_FreeWithoutAbsence_DoesNothing()
    {
        var decision = SyncPlanner.Decide(ReplyOwnership.Free, null, null);

        await Assert.That(decision).IsTypeOf<SyncDecision.NoChange>();
    }
}
