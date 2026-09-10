using CyberGoal.Core.Rules;
using UnityEngine;

namespace CyberGoal.Unity.Core
{
    /// <summary>
    /// Feeds Unity's clock to the Core match director.
    /// </summary>
    /// <remarks>
    /// The entire bridge between engine time and match time, and it is three lines
    /// of substance on purpose. Everything about <em>what happens next</em> lives
    /// in <see cref="MatchDirector"/>, which has no Unity reference and is covered
    /// by tests; this only decides when to tell it that time has passed.
    /// <para>
    /// <c>unscaledDeltaTime</c> is used so that the slow motion §23 and §24 ask for
    /// — implemented by lowering <c>Time.timeScale</c> — slows the <em>visuals</em>
    /// without also stretching the shot clock. A player should not get four extra
    /// seconds to aim because the previous goal was spectacular.
    /// </para>
    /// </remarks>
    public sealed class MatchRunner : MonoBehaviour
    {
        [Tooltip("0 uses a time-based seed. Any other value replays the same match.")]
        public int seed;

        public MatchDirector Director { get; private set; }

        public event System.Action<GameState> StateChanged;

        private void Awake()
        {
            int actual = seed != 0 ? seed : System.Environment.TickCount;
            Director = new MatchDirector(actual);
            Director.StateChanged += OnDirectorStateChanged;
            Debug.Log($"[CyberGoal] match seed {actual}");
        }

        private void OnDestroy()
        {
            if (Director != null) Director.StateChanged -= OnDirectorStateChanged;
        }

        private void OnDirectorStateChanged(GameState state) => StateChanged?.Invoke(state);

        private void Update()
        {
            // Clamped: a backgrounded app hands back a delta of many seconds on
            // resume, and spending it would fast-forward through the whistle and
            // take the player's penalty for them. §57 lists exactly this case.
            float delta = Mathf.Min(0.1f, Time.unscaledDeltaTime);
            Director.Tick(delta);
        }

        private void OnApplicationPause(bool paused)
        {
            // §57: the player backgrounds the app mid-penalty. The shot clock must
            // not keep running while the phone is in a pocket.
            if (paused) Director.Tick(0);
        }
    }
}
