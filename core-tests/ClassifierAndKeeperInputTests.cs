using CyberGoal.Core.Input;
using CyberGoal.Core.Keeper;
using CyberGoal.Core.Physics;
using CyberGoal.Core.Rules;
using CyberGoal.Core.Util;
using NUnit.Framework;

namespace CyberGoal.Core.Tests
{
    [TestFixture]
    public class StrikeClassifierTests
    {
        private static KickAttempt Goal(double crossX, double crossY, double power = 70,
            double curl = 0, bool perfect = false, double margin = 1.0)
            => new KickAttempt(
                Side.Home,
                new KickInput(crossX, crossY, power, curl, perfect, Stance.Driven),
                new KeeperCommit(DiveDirection.LeftLow, 0.1, 0),
                Outcome.Goal, KickResult.Goal,
                new Vec3(crossX, crossY, -BallPhysics.Field.SpotToGoal),
                margin);

        [Test]
        public void AnOrdinaryGoalIsOrdinary()
        {
            Assert.That(StrikeClassifier.Grade(Goal(0.5, 0.9)), Is.EqualTo(StrikeGrade.Standard));
            Assert.That(StrikeClassifier.LabelFor(StrikeGrade.Standard), Is.EqualTo("GOAL"));
        }

        [Test]
        public void ATopCornerThatBeatTheKeeperIsPrecision()
        {
            Assert.That(StrikeClassifier.Grade(Goal(3.2, 2.1, margin: 1.4)),
                Is.EqualTo(StrikeGrade.TopCorner));
        }

        [Test]
        public void ATopCornerTheKeeperNearlyReachedIsNot()
        {
            // The keeper got a hand near it. Impressive placement, but the drama
            // was the near-save, and calling it PRECISION STRIKE overstates it.
            Assert.That(StrikeClassifier.Grade(Goal(3.2, 2.1, margin: 0.05)),
                Is.Not.EqualTo(StrikeGrade.TopCorner));
        }

        [Test]
        public void PerfectNeedsEverythingAtOnce()
        {
            Assert.That(StrikeClassifier.Grade(Goal(3.2, 2.1, perfect: true, margin: 1.4)),
                Is.EqualTo(StrikeGrade.Perfect));

            // Perfect timing alone, into the middle of the goal, is not it.
            Assert.That(StrikeClassifier.Grade(Goal(0.2, 1.0, perfect: true, margin: 1.4)),
                Is.Not.EqualTo(StrikeGrade.Perfect));
        }

        [Test]
        public void CurveOutranksPower()
        {
            // A shot that was both bent and hammered is announced by the bend: it is
            // the more impressive thing to have done deliberately.
            Assert.That(StrikeClassifier.Grade(Goal(1.0, 1.0, power: 96, curl: 0.9)),
                Is.EqualTo(StrikeGrade.Curve));
        }

        [Test]
        public void APoweredShotIsGradedOnPace()
        {
            Assert.That(StrikeClassifier.Grade(Goal(1.0, 1.0, power: 96)),
                Is.EqualTo(StrikeGrade.Power));
        }

        [Test]
        public void NothingButAGoalIsGraded()
        {
            var saved = new KickAttempt(
                Side.Home, new KickInput(3.2, 2.1, 99, 0.9, true, Stance.Driven),
                new KeeperCommit(DiveDirection.RightHigh, 0, 0),
                Outcome.Goal, KickResult.Saved,
                new Vec3(3.2, 2.1, -BallPhysics.Field.SpotToGoal), -0.1);

            Assert.That(StrikeClassifier.Grade(saved), Is.EqualTo(StrikeGrade.Standard));
        }

        [Test]
        public void SlowMotionStaysRare()
        {
            // §24: slow motion must not be overused. Only the two rarest goal
            // grades and a genuine fingertip save earn it.
            Assert.That(StrikeClassifier.DeservesSlowMotion(Goal(0.5, 0.9)), Is.False,
                "a tidy side-foot plays at normal speed");
            Assert.That(StrikeClassifier.DeservesSlowMotion(Goal(1.0, 1.0, power: 96)), Is.False,
                "even a hard shot down the middle");
            Assert.That(StrikeClassifier.DeservesSlowMotion(Goal(3.2, 2.1, margin: 1.4)), Is.True);

            var fingertip = new KickAttempt(
                Side.Home, new KickInput(2.8, 1.0, 80, 0, false, Stance.Driven),
                new KeeperCommit(DiveDirection.RightLow, 0, 0),
                Outcome.Goal, KickResult.Saved,
                new Vec3(2.8, 1.0, -BallPhysics.Field.SpotToGoal), -0.05);
            Assert.That(StrikeClassifier.DeservesSlowMotion(fingertip), Is.True);
        }

        [Test]
        public void EveryGradeHasALabel()
        {
            foreach (StrikeGrade grade in System.Enum.GetValues(typeof(StrikeGrade)))
            {
                Assert.That(StrikeClassifier.LabelFor(grade), Is.Not.Null.And.Not.Empty);
            }
        }
    }

    [TestFixture]
    public class KeeperGestureTests
    {
        private static SwipeSample At(double x, double y) => new SwipeSample(x, y, 0);

        private static KeeperGestureResult From(double dx, double dy)
            => KeeperGesture.Read(At(0.5, 0.5), At(0.5 + dx, 0.5 + dy));

        [Test]
        public void ATapMeansStayUp()
        {
            KeeperGestureResult result = From(0.005, 0.004);
            Assert.That(result.Valid, Is.True);
            Assert.That(result.Direction, Is.Null, "a tap is a centre block, not a dive");
        }

        [Test]
        public void ATinyDragIsNotYetDecidable()
        {
            // Between a tap and a commit there is a band where the player has not
            // said anything yet. Guessing here would fire dives the player never
            // asked for.
            KeeperGestureResult result = From(0.04, 0.0);
            Assert.That(result.Valid, Is.False);
        }

        [Test]
        public void SidewaysFlicksDiveThatWay()
        {
            Assert.That(From(-0.2, 0.0).Direction, Is.EqualTo(DiveDirection.LeftLow));
            Assert.That(From(0.2, 0.0).Direction, Is.EqualTo(DiveDirection.RightLow));
        }

        [Test]
        public void DiagonalFlicksDiveHigh()
        {
            Assert.That(From(0.18, 0.16).Direction, Is.EqualTo(DiveDirection.RightHigh));
            Assert.That(From(-0.18, 0.16).Direction, Is.EqualTo(DiveDirection.LeftHigh));
        }

        [Test]
        public void StraightUpIsAHighCentralSave()
        {
            Assert.That(From(0.01, 0.22).Direction, Is.EqualTo(DiveDirection.CentreHigh));
        }

        [Test]
        public void ASlightlyDiagonalFlickStillCountsAsHigh()
        {
            // The vertical split sits well below 45 degrees on purpose: a thumb
            // flick is mostly horizontal even when the player means "up", and an
            // even split would make high dives almost impossible to ask for.
            KeeperGestureResult result = From(0.20, 0.11);
            Assert.That(result.Direction, Is.EqualTo(DiveDirection.RightHigh),
                "a shallow diagonal must still read as a high dive");
        }

        [Test]
        public void CommitTimeFlowsStraightIntoTheReachModel()
        {
            // §14 and §15 describe one mechanic. A human keeper who dives late must
            // be short of full stretch by exactly the same rule the AI obeys.
            KeeperCommit early = KeeperGesture.ToCommit(DiveDirection.RightLow, 0.02, 0);
            KeeperCommit late = KeeperGesture.ToCommit(DiveDirection.RightLow, 0.5, 0);

            Assert.That(Goalkeeper.ExtensionAt(early, 0.7),
                Is.GreaterThan(Goalkeeper.ExtensionAt(late, 0.7)));
        }

        [Test]
        public void AHumanKeeperCannotStartBeyondTheLineLimit()
        {
            KeeperCommit commit = KeeperGesture.ToCommit(DiveDirection.RightLow, 0.1, 99);
            Assert.That(commit.LinePosition, Is.EqualTo(Goalkeeper.Reach.LineLimit));
        }

        [Test]
        public void ANegativeCommitTimeIsClampedNotTrusted()
        {
            // A clock that runs backwards would grant extra extension. Cheap to
            // rule out here; expensive to find later.
            KeeperCommit commit = KeeperGesture.ToCommit(DiveDirection.RightLow, -3, 0);
            Assert.That(commit.CommittedAt, Is.EqualTo(0));
        }
    }
}
