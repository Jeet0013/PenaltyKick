using UnityEngine;

namespace CyberGoal.Unity.UI
{
    /// <summary>
    /// Keeps a RectTransform inside the device's safe area (§28).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Covers notches, Dynamic Island, punch-hole cameras, curved edges and the
    /// home indicator. §28 is explicit that important UI must never sit behind any
    /// of them, and on a landscape phone — which §2 makes the primary orientation —
    /// the notch eats a strip of one <em>side</em>, exactly where a scoreboard or a
    /// control naturally wants to live.
    /// </para>
    /// <para>
    /// Re-applied on orientation and resolution change rather than only at start,
    /// because the safe area is different in each orientation and §57 lists an
    /// unexpected orientation change as a case to handle. Recomputed only when the
    /// values actually differ, so it costs nothing per frame.
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        private RectTransform _rect;
        private Rect _lastSafeArea;
        private ScreenOrientation _lastOrientation;
        private Vector2Int _lastResolution;

        private void Awake()
        {
            _rect = GetComponent<RectTransform>();
            Apply();
        }

        private void Update()
        {
            if (Screen.safeArea != _lastSafeArea
                || Screen.orientation != _lastOrientation
                || Screen.width != _lastResolution.x
                || Screen.height != _lastResolution.y)
            {
                Apply();
            }
        }

        private void Apply()
        {
            _lastSafeArea = Screen.safeArea;
            _lastOrientation = Screen.orientation;
            _lastResolution = new Vector2Int(Screen.width, Screen.height);

            if (Screen.width <= 0 || Screen.height <= 0) return;

            Vector2 min = _lastSafeArea.position;
            Vector2 max = min + _lastSafeArea.size;
            min.x /= Screen.width;
            min.y /= Screen.height;
            max.x /= Screen.width;
            max.y /= Screen.height;

            // A degenerate safe area — reported by some emulators and by editor
            // simulator devices mid-resize — would collapse the whole UI to nothing.
            if (max.x <= min.x || max.y <= min.y) return;

            _rect.anchorMin = min;
            _rect.anchorMax = max;
            _rect.offsetMin = Vector2.zero;
            _rect.offsetMax = Vector2.zero;
        }
    }
}
