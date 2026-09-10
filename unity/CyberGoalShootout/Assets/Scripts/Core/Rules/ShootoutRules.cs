using System;
using System.Collections.Generic;
using System.Linq;

namespace CyberGoal.Core.Rules
{
    /// <summary>
    /// The rules of a penalty shootout, as pure functions.
    /// </summary>
    /// <remarks>
    /// No rendering, no MonoBehaviour, no timers. Feed it kicks, ask it
    /// questions. That is what makes the two awkward parts — early clinch (§7)
    /// and sudden death (§8) — testable directly rather than by playing a match
    /// and hoping.
    /// </remarks>
    public static class ShootoutRules
    {
        public static MatchState CreateMatch(
            Side firstKicker, ShootoutFormat format = ShootoutFormat.Standard)
            => new MatchState(MatchState.NoKicks, firstKicker, Phase.Standard, null, format);

        public static int ScoreOf(MatchState state, Side side)
            => state.Kicks.Count(k => k.By == side && Shootout.IsGoal(k.Result));

        public static int KicksTakenBy(MatchState state, Side side)
            => state.Kicks.Count(k => k.By == side);

        /// <summary>Kicks a side still has in the standard phase. Zero in sudden death.</summary>
        public static int KicksRemaining(MatchState state, Side side)
        {
            if (state.Phase != Phase.Standard) return 0;
            return Math.Max(0, state.KicksPerSide - KicksTakenBy(state, side));
        }

        /// <summary>
        /// Whose turn it is.
        /// </summary>
        /// <remarks>
        /// Strict alternation from the coin toss winner. In sudden death the same
        /// alternation continues, which is why this does not need to know the
        /// phase: the side with fewer kicks taken is up, and the first kicker
        /// breaks the tie.
        /// </remarks>
        public static Side SideToKick(MatchState state)
        {
            Side first = state.FirstKicker;
            Side second = Shootout.Other(first);
            return KicksTakenBy(state, first) <= KicksTakenBy(state, second) ? first : second;
        }

        /// <summary>
        /// Whether the match is already decided with kicks still to take (§7).
        /// </summary>
        /// <remarks>
        /// <para>
        /// The rule people get wrong. A side is safe when its score is beyond what
        /// the opponent could reach even by scoring every remaining kick — and the
        /// opponent has to be unable to <em>equal</em> it, not merely unable to
        /// pass it, because a tie sends the match to sudden death rather than
        /// ending it. Hence <c>&gt;</c> and not <c>&gt;=</c>; that single character
        /// is the whole rule.
        /// </para>
        /// <para>
        /// Checked after every kick, not only at the end. A shootout that runs its
        /// full ten kicks when the seventh already settled it is a shootout nobody
        /// is still watching.
        /// </para>
        /// </remarks>
        private static Side? ClinchedBy(MatchState state)
        {
            if (state.Phase != Phase.Standard) return null;

            foreach (Side side in new[] { Side.Home, Side.Away })
            {
                Side them = Shootout.Other(side);
                int bestTheyCanReach = ScoreOf(state, them) + KicksRemaining(state, them);
                if (ScoreOf(state, side) > bestTheyCanReach) return side;
            }
            return null;
        }

        /// <summary>
        /// Whether sudden death has produced a winner (§8).
        /// </summary>
        /// <remarks>
        /// The condition is specific: both sides must have taken the same number
        /// of kicks — a <em>completed round</em> — and one must have scored where
        /// the other did not. Checking after a single kick would end the match the
        /// moment the first player of a round scores, which is not sudden death,
        /// it is a coin toss.
        /// </remarks>
        private static Side? SuddenDeathWinner(MatchState state)
        {
            if (state.Phase != Phase.SuddenDeath) return null;

            int homeKicks = KicksTakenBy(state, Side.Home);
            int awayKicks = KicksTakenBy(state, Side.Away);
            if (homeKicks != awayKicks) return null;
            if (homeKicks <= state.KicksPerSide) return null;

            // Only the round just completed can decide it; every earlier round was
            // level or the match would already be over.
            List<KickRecord> round = state.Kicks.Skip(Math.Max(0, state.Kicks.Count - 2)).ToList();
            if (round.Count < 2) return null;

            KickRecord home = round.First(k => k.By == Side.Home);
            KickRecord away = round.First(k => k.By == Side.Away);

            bool homeScored = Shootout.IsGoal(home.Result);
            bool awayScored = Shootout.IsGoal(away.Result);

            if (homeScored && !awayScored) return Side.Home;
            if (awayScored && !homeScored) return Side.Away;
            return null;
        }

        /// <summary>Both sides have used their five.</summary>
        private static bool StandardPhaseExhausted(MatchState state)
            => KicksTakenBy(state, Side.Home) >= state.KicksPerSide
            && KicksTakenBy(state, Side.Away) >= state.KicksPerSide;

        /// <summary>
        /// Record a kick and re-derive the match.
        /// </summary>
        /// <remarks>
        /// The order matters: a clinch is checked <em>before</em> the phase
        /// advances, because a match decided on the tenth kick must not first be
        /// promoted to sudden death and then ended.
        /// </remarks>
        public static MatchState RecordKick(MatchState state, KickResult result)
        {
            if (state.Phase == Phase.Complete)
            {
                throw new InvalidOperationException("RecordKick: the match is already over");
            }

            Side by = SideToKick(state);
            var kicks = new List<KickRecord>(state.Kicks)
            {
                new KickRecord(by, result, KicksTakenBy(state, by) + 1)
            };

            var next = new MatchState(kicks, state.FirstKicker, state.Phase, null, state.Format);

            Side? clinched = ClinchedBy(next);
            if (clinched.HasValue)
            {
                return new MatchState(kicks, state.FirstKicker, Phase.Complete, clinched, state.Format);
            }

            if (next.Phase == Phase.Standard && StandardPhaseExhausted(next))
            {
                int home = ScoreOf(next, Side.Home);
                int away = ScoreOf(next, Side.Away);
                if (home != away)
                {
                    return new MatchState(kicks, state.FirstKicker, Phase.Complete,
                        home > away ? Side.Home : Side.Away, state.Format);
                }
                // Level after five each: the match continues, one round at a time.
                return new MatchState(kicks, state.FirstKicker, Phase.SuddenDeath, null, state.Format);
            }

            Side? sudden = SuddenDeathWinner(next);
            if (sudden.HasValue)
            {
                return new MatchState(kicks, state.FirstKicker, Phase.Complete, sudden, state.Format);
            }

            return next;
        }

        /// <summary>
        /// The pips the HUD draws for one side.
        /// </summary>
        /// <remarks>
        /// Returned by the rules rather than assembled in the UI, so what is
        /// displayed and what is scored cannot drift apart. Sudden-death kicks are
        /// appended past the fifth, which is why the length is not fixed.
        /// </remarks>
        public static IReadOnlyList<Pip> PipsFor(MatchState state, Side side)
        {
            var pips = state.Kicks
                .Where(k => k.By == side)
                .Select(k => Shootout.IsGoal(k.Result) ? Pip.Goal : Pip.Miss)
                .ToList();

            while (pips.Count < state.KicksPerSide) pips.Add(Pip.Pending);
            return pips;
        }

        public static (int Home, int Away) Scoreline(MatchState state)
            => (ScoreOf(state, Side.Home), ScoreOf(state, Side.Away));
    }
}
