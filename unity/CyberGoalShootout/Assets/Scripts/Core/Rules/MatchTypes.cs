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
        /// <summary>Quick or Standard (§5). Fixed for the life of the match.</summary>
        public ShootoutFormat Format { get; }

        /// <summary>Kicks each side gets in the standard phase.</summary>
        public int KicksPerSide => Shootout.KicksFor(Format);

        public MatchState(IReadOnlyList<KickRecord> kicks, Side firstKicker, Phase phase,
            Side? winner, ShootoutFormat format = ShootoutFormat.Standard)
        {
            Kicks = kicks;
            FirstKicker = firstKicker;
            Phase = phase;
            Winner = winner;
            Format = format;
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

    /// <summary>The match formats of §5.</summary>
    public enum ShootoutFormat
    {
        /// <summary>Three each. §5's QUICK SHOOTOUT, 1-2 minutes.</summary>
        Quick,
        /// <summary>Five each. §5's STANDARD SHOOTOUT, 3-5 minutes.</summary>
        Standard
    }

    public static class Shootout
    {
        /// <summary>
        /// Five each in the standard phase, per §6.
        /// </summary>
        /// <remarks>
        /// Kept as the default rather than the only answer. §5 also specifies a
        /// three-kick Quick Shootout, and the number of kicks is exactly the kind
        /// of rule that gets hard-coded in eleven places and then cannot be
        /// changed — which is why <see cref="MatchState"/> now carries its format
        /// and every rule reads it from there.
        /// </remarks>
        public const int KicksPerSide = 5;

        public static int KicksFor(ShootoutFormat format)
            => format == ShootoutFormat.Quick ? 3 : 5;

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
