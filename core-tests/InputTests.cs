using System.Collections.Generic;
using CyberGoal.Core.Input;
using CyberGoal.Core.Physics;
using NUnit.Framework;

namespace CyberGoal.Core.Tests
{
    [TestFixture]
    public class SwipeAnalysisTests
    {
        /// <summary>A straight swipe from (0.5, 0.2) toward a target, over a duration.</summary>
        private static List<SwipeSample> Straight(double toX, double toY, double duration, int steps = 12)
        {
            var samples = new List<SwipeSample>();
            const double fromX = 0.5, fromY = 0.2;
            for (int i = 0; i <= steps; i++)
            {
                double t = i / (double)steps;
                samples.Add(new SwipeSample(
                    fromX + (toX - fromX) * t,
                    fromY + (toY - fromY) * t,
                    t * duration));
            }
            return samples;
        }

        /// <summary>An arcing swipe. Positive bow curves to the screen-right.</summary>
        private static List<SwipeSample> Arced(double toX, double toY, double duration, double bow, int steps = 16)
        {
            var samples = new List<SwipeSample>();
            const double fromX = 0.5, fromY = 0.2;
            for (int i = 0; i <= steps; i++)
            {
                double t = i / (double)steps;
                // Perpendicular displacement peaking at the midpoint.
                double push = System.Math.Sin(t * System.Math.PI) * bow;
                samples.Add(new SwipeSample(
                    fromX + (toX - fromX) * t + push,
                    fromY + (toY - fromY) * t,
                    t * duration));
            }
            return samples;
        }

        [Test]
        public void ATapIsNotAShot()
        {
            var tap = new List<SwipeSample>
            {
                new SwipeSample(0.5, 0.2, 0),
                new SwipeSample(0.505, 0.203, 0.05)
            };
            Assert.That(SwipeAnalysis.Analyse(tap).Valid, Is.False);
        }

        [Test]
        public void ADownwardSwipeIsRejected()
        {
            SwipeResult result = SwipeAnalysis.Analyse(Straight(0.5, 0.05, 0.25));
            Assert.That(result.Valid, Is.False);
            Assert.That(result.Rejection, Does.Contain("toward the goal"));
        }

        [Test]
        public void ASlowDragIsRejected()
        {
            SwipeResult result = SwipeAnalysis.Analyse(Straight(0.5, 0.8, 2.0));
            Assert.That(result.Valid, Is.False);
        }

        [Test]
        public void SwipeDirectionDecidesWhereTheShotGoes()
        {
            SwipeResult left = SwipeAnalysis.Analyse(Straight(0.2, 0.7, 0.25));
            SwipeResult right = SwipeAnalysis.Analyse(Straight(0.8, 0.7, 0.25));

            Assert.That(left.Valid, Is.True);
            Assert.That(right.Valid, Is.True);
            Assert.That(left.TargetX, Is.LessThan(0), "swipe left, shoot left");
            Assert.That(right.TargetX, Is.GreaterThan(0), "swipe right, shoot right");
        }

        [Test]
        public void FasterSwipesHitHarder()
        {
            SwipeResult slow = SwipeAnalysis.Analyse(Straight(0.5, 0.75, 0.9));
            SwipeResult fast = SwipeAnalysis.Analyse(Straight(0.5, 0.75, 0.18));

            Assert.That(slow.Valid, Is.True);
            Assert.That(fast.Valid, Is.True);
            Assert.That(fast.Power, Is.GreaterThan(slow.Power));
        }

        [Test]
        public void PowerComesFromSpeedNotFromReach()
        {
            // Otherwise power becomes a function of hand size and screen height,
            // which would make the control unfair across devices — §2's whole point.
            SwipeResult shortFast = SwipeAnalysis.Analyse(Straight(0.5, 0.45, 0.10));
            SwipeResult longSlow = SwipeAnalysis.Analyse(Straight(0.5, 0.95, 0.85));

            Assert.That(shortFast.Power, Is.GreaterThan(longSlow.Power));
        }

        [Test]
        public void AStraightSwipeDoesNotCurl()
        {
            SwipeResult result = SwipeAnalysis.Analyse(Straight(0.5, 0.8, 0.25));
            Assert.That(result.Valid, Is.True);
            Assert.That(result.Curl, Is.EqualTo(0).Within(0.06));
        }

        [Test]
        public void AnArcedSwipeCurlsAndTheSignsAreOpposite()
        {
            SwipeResult bowRight = SwipeAnalysis.Analyse(Arced(0.5, 0.8, 0.3, 0.09));
            SwipeResult bowLeft = SwipeAnalysis.Analyse(Arced(0.5, 0.8, 0.3, -0.09));

            Assert.That(bowRight.Valid, Is.True);
            Assert.That(bowLeft.Valid, Is.True);
            Assert.That(System.Math.Abs(bowRight.Curl), Is.GreaterThan(0.2), "an arc must bend it");
            Assert.That(bowRight.Curl * bowLeft.Curl, Is.LessThan(0),
                "mirrored arcs must curl opposite ways");
        }

        [Test]
        public void AimStaysWithinReachOfTheFrame()
        {
            // A player must be able to miss, but not to aim into the crowd.
            SwipeResult extreme = SwipeAnalysis.Analyse(Straight(0.0, 1.0, 0.2));
            double limit = BallPhysics.Field.GoalWidth / 2 + SwipeAnalysis.Tuning.OvershootMetres;

            Assert.That(extreme.TargetX, Is.GreaterThanOrEqualTo(-limit));
            Assert.That(extreme.TargetX, Is.LessThanOrEqualTo(limit));
            Assert.That(extreme.TargetY, Is.GreaterThan(0));
        }
    }

    [TestFixture]
    public class TimingWindowTests
    {
        [Test]
        public void TheMarkerSweepsAndReturnsWithoutJumping()
        {
            // A sawtooth teleports from 1 back to 0, which is unreadable at speed
            // and feels like the game cheated. The wave must be continuous.
            double previous = TimingWindow.MarkerAt(0);
            for (double t = 0; t < 4; t += 1.0 / 120)
            {
                double now = TimingWindow.MarkerAt(t);
                Assert.That(System.Math.Abs(now - previous), Is.LessThan(0.05),
                    $"discontinuity at t={t}");
                Assert.That(now, Is.InRange(0, 1));
                previous = now;
            }
        }

        [Test]
        public void DeadCentreIsPerfect()
        {
            TimingResult result = TimingWindow.EvaluateAt(0.5);
            Assert.That(result.IsPerfect, Is.True);
            Assert.That(result.Quality, Is.GreaterThan(0.93));
        }

        [Test]
        public void ZonesAreSymmetricAboutTheCentre()
        {
            Assert.That(TimingWindow.EvaluateAt(0.35).Zone, Is.EqualTo(TimingZone.GoodEarly));
            Assert.That(TimingWindow.EvaluateAt(0.65).Zone, Is.EqualTo(TimingZone.GoodLate));
            Assert.That(TimingWindow.EvaluateAt(0.05).Zone, Is.EqualTo(TimingZone.BadEarly));
            Assert.That(TimingWindow.EvaluateAt(0.95).Zone, Is.EqualTo(TimingZone.BadLate));
        }

        [Test]
        public void QualityFallsAwayFromTheCentre()
        {
            Assert.That(TimingWindow.EvaluateAt(0.5).Quality,
                Is.GreaterThan(TimingWindow.EvaluateAt(0.3).Quality));
            Assert.That(TimingWindow.EvaluateAt(0.3).Quality,
                Is.GreaterThan(TimingWindow.EvaluateAt(0.02).Quality));
        }

        [Test]
        public void IgnoringTheMeterIsNotPunished()
        {
            // §11 calls the mechanic optional, and a mechanic you are punished for
            // declining is not optional.
            TimingResult unused = TimingWindow.Unused();
            Assert.That(unused.Quality, Is.GreaterThan(TimingWindow.EvaluateAt(0.02).Quality),
                "declining must beat mistiming badly");
            Assert.That(unused.Quality, Is.LessThan(TimingWindow.EvaluateAt(0.5).Quality),
                "but must not beat hitting it");
        }

        [Test]
        public void PoorTimingPullsTheShotTowardTheMiddle()
        {
            // Bad contact should read as a scuff, not as the game overriding aim.
            var (goodX, _) = TimingWindow.ApplyAccuracy(3.0, 2.0, TimingWindow.EvaluateAt(0.5), 1.0);
            var (badX, _) = TimingWindow.ApplyAccuracy(3.0, 2.0, TimingWindow.EvaluateAt(0.02), 1.0);

            Assert.That(goodX, Is.EqualTo(3.0).Within(0.25));
            Assert.That(badX, Is.LessThan(goodX), "a mistimed shot drifts back toward centre");
            Assert.That(badX, Is.GreaterThan(0), "but keeps the side the player chose");
        }
    }
}
