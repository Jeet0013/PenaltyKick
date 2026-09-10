using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace CyberGoal.Core.Rules
{
    public enum Side
    {
        Home,
        Away
    }

    /// <summary>How a single penalty ended.</summary>
    public enum KickResult
    {
        Goal,
        Saved,
        OffTarget,
        /// <summary>Struck the frame and stayed out. Worth nothing, per §6.</summary>
        Woodwork,
        /// <summary>The striker let the shot clock run out.</summary>
        Expired
    }

    public enum Phase
    {
        Standard,
        SuddenDeath,
        Complete
    }

    public readonly struct KickRecord
    {
        public readonly Side By;
        public readonly KickResult Result;
        /// <summary>1-based, per side.</summary>
        public readonly int Ordinal;

        public KickRecord(Side by, KickResult result, int ordinal)
        {
            By = by;
            Result = result;
            Ordinal = ordinal;
        }
    }

    /// <summary>
    /// The whole match, as an immutable value.
    /// </summary>
    /// <remarks>
    /// Immutable on purpose: <see cref="ShootoutRules.RecordKick"/> returns a new
    /// state rather than mutating this one. It means the match cannot be
    /// corrupted by a view holding a stale reference, a replay can hold every
    /// intermediate state at once, and a networked client can diff two states to
    /// find what changed instead of trusting an event to have fired.
    /// </remarks>
    public sealed class MatchState
    {
        public IReadOnlyList<KickRecord> Kicks { get; }
        /// <summary>Who takes the first kick, from the coin toss.</summary>
        public Side FirstKicker { get; }
        public Phase Phase { get; }
        /// <summary>Set only once the match is over.</summary>
        public Side? Winner { get; }

        public MatchState(IReadOnlyList<KickRecord> kicks, Side firstKicker, Phase phase, Side? winner)
        {
            Kicks = kicks;
            FirstKicker = firstKicker;
            Phase = phase;
            Winner = winner;
        }

        public static readonly IReadOnlyList<KickRecord> NoKicks =
            new ReadOnlyCollection<KickRecord>(new List<KickRecord>());
    }

    /// <summary>What one pip on the scoreboard shows.</summary>
    public enum Pip
    {
        Goal,
        Miss,
        Pending
    }

    public static class Shootout
    {
        /// <summary>Five each in the standard phase, per §6.</summary>
        public const int KicksPerSide = 5;

        /// <summary>The striker's clock, per §57's "match ends before all penalties" case.</summary>
        public const double ShotClockSeconds = 8.0;

        public static Side Other(Side side) => side == Side.Home ? Side.Away : Side.Home;

        /// <summary>
        /// Only a goal scores. Named rather than compared inline because §6 lists
        /// four separate ways to score nothing, and one place to change it is the
        /// difference between a rule and a habit.
        /// </summary>
        public static bool IsGoal(KickResult result) => result == KickResult.Goal;
    }
}
