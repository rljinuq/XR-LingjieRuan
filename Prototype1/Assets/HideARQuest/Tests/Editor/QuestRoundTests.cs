using NUnit.Framework;

namespace HideAR.Quest.Tests
{
    public class QuestRoundTests
    {
        static QuestRound ReviewRound()
        {
            var round = new QuestRound();
            Assert.IsTrue(round.StartHiding());
            round.UpdateRelease(false, 0.4f);
            Assert.IsTrue(round.RequestPlacement());
            Assert.IsTrue(round.AcceptAnchor());
            return round;
        }

        [Test] public void StartButtonCannotAlsoPlace()
        {
            var round = new QuestRound();
            round.StartHiding();
            Assert.IsFalse(round.RequestPlacement());
            round.UpdateRelease(true, 1);
            Assert.IsFalse(round.RequestPlacement());
            round.UpdateRelease(false, 0.2f);
            Assert.IsFalse(round.RequestPlacement());
            round.UpdateRelease(false, 0.2f);
            Assert.IsTrue(round.RequestPlacement());
        }

        [Test] public void BothTriggersCannotCreateTwoAnchors()
        {
            var round = new QuestRound();
            round.StartHiding();
            round.UpdateRelease(false, 1);
            Assert.IsTrue(round.RequestPlacement());
            Assert.IsFalse(round.RequestPlacement());
            Assert.IsTrue(round.AcceptAnchor());
            Assert.IsFalse(round.AcceptAnchor());
            round.UpdateRelease(false, 1);
            Assert.IsFalse(round.RequestPlacement());
        }

        [Test] public void FreshPressRemainsArmedUntilPlacementConsumesIt()
        {
            var round = new QuestRound();
            round.StartHiding();
            round.UpdateRelease(false, 0.4f);
            round.UpdateRelease(true, 0.016f);
            Assert.IsTrue(round.RequestPlacement());
            Assert.IsFalse(round.InputArmed);
            Assert.IsFalse(round.RequestPlacement());
        }

        [Test] public void PressingDuringReleaseDelayRestartsTheDelay()
        {
            var round = new QuestRound();
            round.StartHiding();
            round.UpdateRelease(false, 0.3f);
            round.UpdateRelease(true, 0.016f);
            round.UpdateRelease(false, 0.1f);
            Assert.IsFalse(round.RequestPlacement());
            round.UpdateRelease(false, 0.3f);
            Assert.IsTrue(round.RequestPlacement());
        }

        [Test] public void PlacementWaitsForConfirmation()
        {
            var round = ReviewRound();
            round.Tick(30, true);
            Assert.AreEqual(QuestPhase.Review, round.Phase);
            Assert.IsTrue(round.Confirm());
            round.Tick(30, true);
            Assert.AreEqual(QuestPhase.Handoff, round.Phase);
        }

        [Test] public void MovingRequiresFreshTriggerRelease()
        {
            var round = ReviewRound();
            Assert.IsTrue(round.Move());
            Assert.IsFalse(round.RequestPlacement());
            round.UpdateRelease(false, 0.4f);
            Assert.IsTrue(round.RequestPlacement());
        }

        [Test] public void HandoffDoesNotStartTimer()
        {
            var round = ReviewRound();
            round.Confirm();
            round.Tick(90, true);
            Assert.AreEqual(0, round.SearchSeconds);
            round.Ready();
            round.Tick(1, true);
            round.Tick(90, false);
            Assert.AreEqual(2, round.CountdownRemaining);
            round.Tick(2, true);
            Assert.AreEqual(QuestPhase.Seeking, round.Phase);
            Assert.AreEqual(0, round.SearchSeconds);
        }

        [Test] public void FoundStopsTimerAndTrackingLossPausesIt()
        {
            var round = ReviewRound();
            round.Confirm(); round.Ready(); round.Tick(3, true);
            Assert.IsFalse(round.Found());
            round.Tick(5, true); round.Tick(60, false);
            Assert.AreEqual(5, round.SearchSeconds);
            round.UpdateRelease(false, 0.4f);
            Assert.IsTrue(round.Found());
            round.Tick(20, true);
            Assert.AreEqual(5, round.SearchSeconds);
        }

        [Test] public void LateAnchorAfterTimeoutIsRejected()
        {
            var round = new QuestRound();
            round.StartHiding(); round.UpdateRelease(false, 1); round.RequestPlacement();
            round.Fail();
            Assert.IsTrue(round.AnchorPending);
            Assert.IsFalse(round.AcceptAnchor());
            Assert.IsFalse(round.StartHiding());
        }

        [Test] public void ResetClearsStateAndDoesNotStartPlacement()
        {
            var round = ReviewRound();
            round.Confirm(); round.Ready(); round.Tick(3, true); round.Tick(10, true);
            round.Reset();
            Assert.AreEqual(QuestPhase.Intro, round.Phase);
            Assert.AreEqual(0, round.SearchSeconds);
            Assert.IsFalse(round.RequestPlacement());
            Assert.IsFalse(round.AcceptAnchor());
            Assert.IsFalse(round.Found());
        }
    }
}
