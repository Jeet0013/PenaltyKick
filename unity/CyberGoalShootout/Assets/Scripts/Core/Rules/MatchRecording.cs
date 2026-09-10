using System.Collections.Generic;
using CyberGoal.Core.Keeper;
using CyberGoal.Core.Physics;

namespace CyberGoal.Core.Rules
{
    /// <summary>One kick, as the minimum needed to reproduce it exactly.</summary>
    public readonly struct RecordedKick
    {
        public readonly KickInput Input;
        public readonly KeeperCommit Keeper;

        public RecordedKick(KickInput input, KeeperCommit keeper)
        {
            Input = input;
            Keeper = keeper;
        }
    }

    /// <summary>
    /// A whole match, as a seed and a list of inputs (§26).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>What is deliberately not stored: any outcome.</b> No trajectories, no
    /// results, no score. Storing them would make a recording a video — bulky,
    /// and unable to answer whether the outcomes were legitimate. Storing only
    /// the inputs makes it a <em>proof</em>: replaying it re-derives every result
    /// from the same physics the live match used, and if the replay disagrees
    /// with what the players saw, something is wrong and the recording is how you
    /// find out.
    /// </para>
    /// <para>
    /// That is the same property §40 needs from a server. A match that can be
    /// re-run from its inputs can be validated by a server from its inputs, and
    /// a client that claims a goal it did not score fails the re-run. §26's
    /// replay camera and §40's authoritative match are the same feature seen from
    /// two directions, which is why this type serves both.
    /// </para>
    /// <para>
    /// It is also tiny: a five-kick shootout is an int and ten small structs. A
    /// recording can go in a save file, a leaderboard entry or a network packet
    /// without anyone thinking about size.
    /// </para>
    /// </remarks>
    public sealed class MatchRecording
    {
        public int Seed { get; }
        public ShootoutFormat Format { get; }
        public IReadOnlyList<RecordedKick> Kicks { get; }

        public MatchRecording(int seed, ShootoutFormat format, IReadOnlyList<RecordedKick> kicks)
        {
            Seed = seed;
            Format = format;
            Kicks = kicks;
        }
    }

    /// <summary>Builds a recording while a match is played.</summary>
    public sealed class MatchRecorder
    {
        private readonly List<RecordedKick> _kicks = new List<RecordedKick>();

        public MatchRecorder(int seed, ShootoutFormat format = ShootoutFormat.Standard)
        {
            Seed = seed;
            Format = format;
        }

        public int Seed { get; }
        public ShootoutFormat Format { get; }
        public int Count => _kicks.Count;

        public void Record(KickInput input, KeeperCommit keeper)
            => _kicks.Add(new RecordedKick(input, keeper));

        public MatchRecording Build() => new MatchRecording(Seed, Format, _kicks.ToArray());
    }

    /// <summary>Re-runs a recording and reports what it produces.</summary>
    public static class MatchReplay
    {
        /// <summary>
        /// Replay every kick, deriving results rather than reading them back.
        /// </summary>
        /// <remarks>
        /// Uses the same <see cref="BallPhysics.Simulate"/> and
        /// <see cref="Goalkeeper.AttemptSave"/> the live match used. Nothing here
        /// knows what happened the first time, which is the whole point: if this
        /// returns a different answer, the difference is real and worth chasing.
        /// </remarks>
        public static IReadOnlyList<KickAttempt> Replay(MatchRecording recording)
        {
            var attempts = new List<KickAttempt>(recording.Kicks.Count);
            MatchState match = ShootoutRules.CreateMatch(
                new Util.Rng(recording.Seed).Chance(0.5) ? Side.Home : Side.Away,
                recording.Format);

            foreach (RecordedKick kick in recording.Kicks)
            {
                if (match.Phase == Phase.Complete) break;

                Side striker = ShootoutRules.SideToKick(match);
                BallState flight = BallPhysics.Simulate(kick.Input);

                KickResult result;
                double margin = 0;

                if (flight.Outcome == Outcome.Goal)
                {
                    SaveAttempt save = Goalkeeper.AttemptSave(flight.Position, kick.Keeper, flight.Elapsed);
                    result = save.Saved ? KickResult.Saved : KickResult.Goal;
                    margin = save.Margin;
                }
                else if (flight.Outcome == Outcome.Woodwork)
                {
                    result = KickResult.Woodwork;
                }
                else
                {
                    result = KickResult.OffTarget;
                }

                attempts.Add(new KickAttempt(
                    striker, kick.Input, kick.Keeper, flight.Outcome, result, flight.Position, margin));

                match = ShootoutRules.RecordKick(match, result);
            }

            return attempts;
        }

        /// <summary>The final match state a recording produces.</summary>
        public static MatchState FinalState(MatchRecording recording)
        {
            MatchState match = ShootoutRules.CreateMatch(
                new Util.Rng(recording.Seed).Chance(0.5) ? Side.Home : Side.Away,
                recording.Format);

            foreach (KickAttempt attempt in Replay(recording))
            {
                if (match.Phase == Phase.Complete) break;
                match = ShootoutRules.RecordKick(match, attempt.Result);
            }
            return match;
        }
    }
}
