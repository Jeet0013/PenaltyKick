using System.Collections.Generic;
using CyberGoal.Core.Input;
using UnityEngine;

namespace CyberGoal.Unity.Input
{
    /// <summary>
    /// Collects a finger or mouse path and hands it to <see cref="SwipeAnalysis"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class does no interpretation at all — it samples, normalises, and
    /// forwards. Every decision about what a swipe <em>means</em> lives in Core,
    /// where it is unit-tested without a device in the room. Splitting it this way
    /// is the difference between tuning the controls from a test run in
    /// milliseconds and tuning them by rebuilding to a phone.
    /// </para>
    /// <para>
    /// Samples are normalised to 0-1 of screen width and height before leaving
    /// here. §2 requires the same gesture to mean the same thing on a 4:3 tablet
    /// and a 20:9 phone, and a swipe measured in pixels is a different swipe on
    /// every device.
    /// </para>
    /// <para>
    /// Uses the legacy <c>UnityEngine.Input</c> rather than the Input System
    /// package deliberately: a raw screen position over time is the entire input
    /// requirement here, both backends expose it identically, and this way the
    /// project runs whether or not the package has finished importing. If richer
    /// binding is wanted later — gamepads, per-§5 local two-player — it replaces
    /// the three calls in <see cref="Sample"/> and nothing else.
    /// </para>
    /// </remarks>
    public sealed class SwipeInput : MonoBehaviour
    {
        [Tooltip("Maximum samples kept per gesture. A long drag is rejected anyway.")]
        public int maxSamples = 96;

        private readonly List<SwipeSample> _samples = new List<SwipeSample>();
        private bool _tracking;
        private bool _enabled = true;

        /// <summary>Fired when a completed gesture parses as a shot.</summary>
        public event System.Action<SwipeResult> SwipeCompleted;

        /// <summary>Fired when a gesture is released but rejected, with the reason.</summary>
        public event System.Action<string> SwipeRejected;

        /// <summary>Fired on first touch, so the timing meter can start sweeping.</summary>
        public event System.Action GestureBegan;

        /// <summary>Whether a gesture is in progress, for the aim preview.</summary>
        public bool IsTracking => _tracking;

        /// <summary>The most recent sample, for drawing a live aim hint.</summary>
        public SwipeSample Latest => _samples.Count > 0 ? _samples[_samples.Count - 1] : default;

        /// <summary>Gate input to the phases that allow it.</summary>
        public void SetEnabled(bool value)
        {
            _enabled = value;
            if (!value) Cancel();
        }

        private void Update()
        {
            if (!_enabled) return;

            if (TryGetPointer(out Vector2 position, out bool down, out bool up))
            {
                if (down) Begin(position);
                else if (_tracking && !up) Sample(position);

                if (up && _tracking) End(position);
            }
        }

        /// <summary>
        /// Read whichever pointer is available.
        /// </summary>
        /// <remarks>
        /// Touch first so a phone never falls through to the mouse emulation
        /// layer, which reports a single synthesised pointer and would silently
        /// break any future two-finger control.
        /// </remarks>
        private static bool TryGetPointer(out Vector2 position, out bool down, out bool up)
        {
            if (UnityEngine.Input.touchCount > 0)
            {
                Touch touch = UnityEngine.Input.GetTouch(0);
                position = touch.position;
                down = touch.phase == TouchPhase.Began;
                up = touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled;
                return true;
            }

            if (UnityEngine.Input.GetMouseButton(0)
                || UnityEngine.Input.GetMouseButtonDown(0)
                || UnityEngine.Input.GetMouseButtonUp(0))
            {
                position = UnityEngine.Input.mousePosition;
                down = UnityEngine.Input.GetMouseButtonDown(0);
                up = UnityEngine.Input.GetMouseButtonUp(0);
                return true;
            }

            position = default;
            down = false;
            up = false;
            return false;
        }

        private void Begin(Vector2 screen)
        {
            _samples.Clear();
            _tracking = true;
            Sample(screen);
            GestureBegan?.Invoke();
        }

        private void Sample(Vector2 screen)
        {
            if (_samples.Count >= maxSamples) return;
            _samples.Add(new SwipeSample(
                screen.x / Mathf.Max(1, Screen.width),
                screen.y / Mathf.Max(1, Screen.height),
                Time.unscaledTime));
        }

        private void End(Vector2 screen)
        {
            Sample(screen);
            _tracking = false;

            SwipeResult result = SwipeAnalysis.Analyse(_samples);
            _samples.Clear();

            if (result.Valid) SwipeCompleted?.Invoke(result);
            else SwipeRejected?.Invoke(result.Rejection);
        }

        public void Cancel()
        {
            _samples.Clear();
            _tracking = false;
        }
    }
}
