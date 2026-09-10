namespace CyberGoal.Core.Rules
{
    /// <summary>
    /// Every state the game can be in, per §49.
    /// </summary>
    /// <remarks>
    /// One enum for the whole application, not one per screen. §49 asks for a
    /// centralized state precisely so that "can the striker kick right now?" has
    /// exactly one answer in exactly one place — with flags scattered through an
    /// input handler that question gets asked in several places and eventually
    /// one of them is wrong.
    /// </remarks>
    public enum GameState
    {
        Menu,
        TeamSelection,
        PlayerSelection,
        MatchIntro,

        /// <summary>Ball being placed, keeper walking to the line.</summary>
        PrePenalty,
        /// <summary>Referee checks position and raises the whistle (§18).</summary>
        RefereeReady,
        /// <summary>The whistle itself. The striker is not yet permitted to kick.</summary>
        Whistle,
        /// <summary>Permission granted. The shot clock runs here.</summary>
        Aiming,
        /// <summary>Swipe released; run-up and strike animation playing.</summary>
        Shooting,
        /// <summary>Ball in flight. Nothing the player does now changes it.</summary>
        BallInPlay,

        GoalResult,
        SaveResult,
        MissResult,

        Replay,
        Celebration,
        NextPenalty,
        SuddenDeath,

        MatchEnd,
        Victory,
        Defeat
    }

    /// <summary>Which of the three result states an outcome maps to.</summary>
    public static class GameStates
    {
        public static GameState ResultFor(KickResult result) => result switch
        {
            KickResult.Goal => GameState.GoalResult,
            KickResult.Saved => GameState.SaveResult,
            // A post, a miss and an expired clock all read to the player as "not
            // scored". They are separate KickResults because the crowd, the
            // commentary and the replay all treat them differently, but the state
            // they lead to is the same one.
            _ => GameState.MissResult
        };

        /// <summary>
        /// Whether the striker may strike. The single source of truth for §18's
        /// rule that the whistle gates the kick.
        /// </summary>
        public static bool CanStrike(GameState state) => state == GameState.Aiming;

        /// <summary>Whether the human keeper may commit a dive.</summary>
        public static bool CanDive(GameState state)
            => state == GameState.Aiming || state == GameState.Shooting;

        public static bool IsResult(GameState state)
            => state == GameState.GoalResult
            || state == GameState.SaveResult
            || state == GameState.MissResult;

        public static bool IsTerminal(GameState state)
            => state == GameState.Victory || state == GameState.Defeat;
    }
}
