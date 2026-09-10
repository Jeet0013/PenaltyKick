using CyberGoal.Core.Keeper;
using CyberGoal.Core.Physics;
using CyberGoal.Core.Util;
using NUnit.Framework;

namespace CyberGoal.Core.Tests
{
    [TestFixture]
    public class GoalkeeperTests
    {
        private static KeeperCommit Dive(DiveDirection d, double at = 0.05, double line = 0)
            => new KeeperCommit(d, at, line);

        private static Vec3 At(double x, double y) => new Vec3(x, y, -BallPhysics.Field.SpotToGoal);

        [Test]
        public void AnEarlyDiveReachesFurtherThanALateOne()
        {
            // The central trade of §15: commit early for reach, late for disguise.
            var early = Dive(DiveDirection.RightLow, at: 0.0);
            var late = Dive(DiveDirection.RightLow, at: 0.45);

            Assert.That(Goalkeeper.ExtensionAt(early, 0.6),
                Is.GreaterThan(Goalkeeper.ExtensionAt(late, 0.6)));
        }

        [Test]
        public void ExtensionIsEasedNotLinear()
        {
            // A linear ramp makes late dives uselessly short and pushes everyone
            // into diving early. Half the extend time must buy well over half the
            // distance.
            var commit = Dive(DiveDirection.RightLow, at: 0);
            double half = Goalkeeper.ExtensionAt(commit, Goalkeeper.Reach.ExtendSeconds / 2);
            Assert.That(half, Is.GreaterThan(0.6));
        }

        [Test]
        public void CannotReachBeyondFullStretch()
        {
            // The hard ceiling that makes §15's "no impossible saves" checkable.
            // Full reach 2.45 + hand 0.62 = 3.07 m; anything past that is a goal
            // no matter how early the keeper went.
            var commit = Dive(DiveDirection.RightLow, at: 0);
            SaveAttempt attempt = Goalkeeper.AttemptSave(At(3.2, 0.5), commit, 1.0);
            Assert.That(attempt.Saved, Is.False);
        }

        [Test]
        public void ReachesAShotJustInsideFullStretch()
        {
            var commit = Dive(DiveDirection.RightLow, at: 0);
            SaveAttempt attempt = Goalkeeper.AttemptSave(At(2.9, 0.5), commit, 1.0);
            Assert.That(attempt.Saved, Is.True);
        }

        [Test]
        public void DivingTheWrongWayConcedes()
        {
            var commit = Dive(DiveDirection.LeftLow, at: 0);
            SaveAttempt attempt = Goalkeeper.AttemptSave(At(2.6, 0.5), commit, 0.9);
            Assert.That(attempt.Saved, Is.False);
        }

        [Test]
        public void ABodyShotDuringADiveIsStillASave()
        {
            // The arm is between the keeper and the reaching hand, so the covered
            // area is a span, not a point at the end of it.
            var commit = Dive(DiveDirection.RightLow, at: 0);
            SaveAttempt attempt = Goalkeeper.AttemptSave(At(0.8, 0.5), commit, 0.9);
            Assert.That(attempt.Saved, Is.True);
        }

        [Test]
        public void AHighShotBeatsALowDive()
        {
            var commit = Dive(DiveDirection.RightLow, at: 0);
            SaveAttempt attempt = Goalkeeper.AttemptSave(At(2.0, 2.2), commit, 0.9);
            Assert.That(attempt.Saved, Is.False, "a low dive cannot cover the top corner");
        }

        [Test]
        public void StandingBlockBeatsTheMiddleAndLosesToTheCorners()
        {
            // If standing up covered the corners too, never committing would be the
            // correct play and the keeper would stop being a decision.
            var stand = new KeeperCommit(null, 0, 0);

            Assert.That(Goalkeeper.AttemptSave(At(0.3, 0.5), stand, 0.9).Saved, Is.True);
            Assert.That(Goalkeeper.AttemptSave(At(2.6, 0.5), stand, 0.9).Saved, Is.False);
        }

        [Test]
        public void MarginReportsHowCloseItWas()
        {
            var commit = Dive(DiveDirection.RightLow, at: 0);
            SaveAttempt beaten = Goalkeeper.AttemptSave(At(3.2, 0.5), commit, 1.0);
            Assert.That(beaten.Margin, Is.GreaterThan(0), "positive means it got past");

            SaveAttempt stopped = Goalkeeper.AttemptSave(At(1.0, 0.5), commit, 1.0);
            Assert.That(stopped.Margin, Is.LessThanOrEqualTo(0), "negative means it was reached");
        }

        [Test]
        public void DiveThatCoversNamesTheRightCorner()
        {
            Assert.That(Goalkeeper.DiveThatCovers(At(-2.5, 2.0)), Is.EqualTo(DiveDirection.LeftHigh));
            Assert.That(Goalkeeper.DiveThatCovers(At(2.5, 0.4)), Is.EqualTo(DiveDirection.RightLow));
            Assert.That(Goalkeeper.DiveThatCovers(At(0.0, 0.4)), Is.EqualTo(DiveDirection.CentreLow));
        }
    }
}
