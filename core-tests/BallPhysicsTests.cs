using System;
using CyberGoal.Core.Physics;
using CyberGoal.Core.Util;
using NUnit.Framework;

namespace CyberGoal.Core.Tests
{
    [TestFixture]
    public class BallPhysicsTests
    {
        private static KickInput Aim(double x, double y, double power = 70,
            double curl = 0, Stance stance = Stance.Placed, bool perfect = false)
            => new KickInput(x, y, power, curl, perfect, stance);

        [Test]
        public void ArrivesWhereItWasAimed()
        {
            // The bug that made the web prototype's reticle a liar: a stance
            // multiplier applied to vy *after* vy had been solved to reach targetY.
            // Aiming at 4.44 m put the ball across at 2.21 m. Every stance must
            // land within a ball's width of the aim point.
            foreach (Stance stance in new[] { Stance.Driven, Stance.Placed, Stance.Finesse, Stance.Chip })
            {
                BallState end = BallPhysics.Simulate(Aim(2.0, 1.6, 75, 0, stance));
                Assert.That(end.Outcome, Is.EqualTo(Outcome.Goal), $"{stance} should be on target");
                Assert.That(end.Position.Y, Is.EqualTo(1.6).Within(0.22),
                    $"{stance} arrived at the wrong height");
                Assert.That(end.Position.X, Is.EqualTo(2.0).Within(0.22),
                    $"{stance} arrived at the wrong width");
            }
        }

        [Test]
        public void IsDeterministic()
        {
            // The property the whole architecture is built around: identical input,
            // identical flight, every time.
            KickInput input = Aim(-2.4, 1.1, 83, 0.6, Stance.Finesse);
            BallState a = BallPhysics.Simulate(input);
            BallState b = BallPhysics.Simulate(input);

            Assert.That(a.Position.X, Is.EqualTo(b.Position.X));
            Assert.That(a.Position.Y, Is.EqualTo(b.Position.Y));
            Assert.That(a.Elapsed, Is.EqualTo(b.Elapsed));
            Assert.That(a.Outcome, Is.EqualTo(b.Outcome));
        }

        [Test]
        public void CurlBendsTheRightWay()
        {
            BallState left = BallPhysics.Simulate(Aim(0, 1.2, 70, -1, Stance.Finesse));
            BallState none = BallPhysics.Simulate(Aim(0, 1.2, 70, 0, Stance.Finesse));
            BallState right = BallPhysics.Simulate(Aim(0, 1.2, 70, 1, Stance.Finesse));

            Assert.That(left.Position.X, Is.LessThan(none.Position.X));
            Assert.That(right.Position.X, Is.GreaterThan(none.Position.X));
        }

        [Test]
        public void FinesseCurlsMoreThanDriven()
        {
            // The stance trade-off has to be real, not decorative.
            double finesse = Math.Abs(BallPhysics.Simulate(Aim(0, 1.2, 70, 1, Stance.Finesse)).Position.X);
            double driven = Math.Abs(BallPhysics.Simulate(Aim(0, 1.2, 70, 1, Stance.Driven)).Position.X);
            Assert.That(finesse, Is.GreaterThan(driven));
        }

        [Test]
        public void ChipIsSlowerAndTakesLonger()
        {
            BallState chip = BallPhysics.Simulate(Aim(0, 1.6, 70, 0, Stance.Chip));
            BallState driven = BallPhysics.Simulate(Aim(0, 1.6, 70, 0, Stance.Driven));
            Assert.That(chip.Elapsed, Is.GreaterThan(driven.Elapsed),
                "a chip gives the keeper more time — that is the whole trade");
        }

        [Test]
        public void AShotOutsideTheFrameIsWide()
        {
            BallState end = BallPhysics.Simulate(Aim(5.6, 1.2, 80));
            Assert.That(end.Outcome, Is.EqualTo(Outcome.Wide));
        }

        [Test]
        public void AShotOverTheBarIsWide()
        {
            BallState end = BallPhysics.Simulate(Aim(0, 3.3, 85));
            Assert.That(end.Outcome, Is.EqualTo(Outcome.Wide));
        }

        [Test]
        public void HittingTheUprightIsWoodwork()
        {
            // Just inside the post: 3.66 is the post centre, so aim at the frame.
            BallState end = BallPhysics.Simulate(Aim(BallPhysics.Field.GoalWidth / 2, 1.2, 80));
            Assert.That(end.Outcome, Is.EqualTo(Outcome.Woodwork));
        }

        [Test]
        public void FastShotsDoNotTunnelThroughTheGoalPlane()
        {
            // At full power the ball moves ~26 cm per step, four times a post's
            // width. Without interpolating the crossing, a shot at the post can
            // step straight past the plane and be scored as a goal.
            BallState end = BallPhysics.Simulate(Aim(0, 1.2, 100, 0, Stance.Driven));
            Assert.That(end.Outcome, Is.Not.EqualTo(Outcome.InFlight),
                "the flight must resolve, not run past the goal");
            Assert.That(end.Position.Z, Is.EqualTo(-BallPhysics.Field.SpotToGoal).Within(1e-9),
                "resolution must report the exact crossing point, not the overshoot");
        }

        [Test]
        public void ALowWeakShotBouncesAndStillCrossesTheLine()
        {
            // Regression for a missing ground plane. With nothing to bounce off,
            // this shot descended through the turf and crossed the goal line at a
            // negative height, which the goal test rejected — a trickling penalty
            // scored as a miss, for a reason nothing in the symptom pointed at.
            BallState end = BallPhysics.Simulate(Aim(0, 0.2, 0, 0, Stance.Chip));

            Assert.That(end.Position.Y, Is.GreaterThanOrEqualTo(0),
                "the ball must never be below the pitch");
            Assert.That(end.Outcome, Is.EqualTo(Outcome.Goal));
        }

        [Test]
        public void TheBallComesToRestInsteadOfBouncingForever()
        {
            // Restitution alone gives infinitely many ever-smaller bounces, so the
            // ball never settles, Short never fires, and the flight burns its whole
            // iteration budget every time. The rolling threshold is what ends it.
            BallState end = BallPhysics.Simulate(
                new KickInput(0, 0.15, 4, 0, false, Stance.Chip), maxSeconds: 6);

            Assert.That(end.Outcome, Is.Not.EqualTo(Outcome.InFlight),
                "a dribbled ball must resolve, not run out the clock");
        }

        [Test]
        public void BouncingScrubsSpin()
        {
            // Turf grips the ball for the moment of contact, which is why a curling
            // shot straightens once it lands.
            BallState launched = BallPhysics.Launch(
                new KickInput(0, 0.14, 20, 1, false, Stance.Finesse));
            BallState ball = launched;
            for (int i = 0; i < 240 && ball.Outcome == Outcome.InFlight; i++)
            {
                ball = BallPhysics.Step(ball);
                if (Math.Abs(ball.Spin) < Math.Abs(launched.Spin) - 1e-9) break;
            }

            Assert.That(Math.Abs(ball.Spin), Is.LessThan(Math.Abs(launched.Spin)));
        }

        [Test]
        public void PerfectContactAddsPaceButNotCertainty()
        {
            BallState plain = BallPhysics.Simulate(Aim(1.5, 1.2, 70, 0, Stance.Driven));
            BallState perfect = BallPhysics.Simulate(Aim(1.5, 1.2, 70, 0, Stance.Driven, perfect: true));
            Assert.That(perfect.Elapsed, Is.LessThan(plain.Elapsed), "perfect contact is faster");
            // And still an ordinary goal that a keeper could have reached.
            Assert.That(perfect.Outcome, Is.EqualTo(Outcome.Goal));
        }
    }
}
