using System.Collections.Generic;
using CyberGoal.Core.Input;
using CyberGoal.Core.Rules;
using CyberGoal.Core.Teams;
using UnityEngine;
using UnityEngine.UI;

namespace CyberGoal.Unity.UI
{
    /// <summary>
    /// The in-match HUD (§27).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Minimal on purpose, and it fades while the ball is in flight. §27 asks for
    /// exactly that, and the reason is that the moment after release is the one the
    /// player most wants to watch — anything overlaying it is in the way.
    /// </para>
    /// <para>
    /// Built with uGUI in code rather than as a prefab, for the same reason the
    /// stadium is: a hand-authored prefab is GUID-laden YAML that fails opaquely,
    /// and this project has to open correctly in an editor nobody has run yet.
    /// </para>
    /// <para>
    /// Pips carry <b>shape as well as colour</b> — filled for a goal, hollow for a
    /// miss, faint for pending. The brief's accessibility requirements list
    /// colourblind support, and a scoreboard where scored and missed differ only in
    /// hue is unreadable to roughly one man in twelve.
    /// </para>
    /// </remarks>
    public sealed class MatchHud : MonoBehaviour
    {
        private Text _score;
        private Text _prompt;
        private Text _subtitle;
        private Text _clock;
        private RectTransform _homePips;
        private RectTransform _awayPips;
        private CanvasGroup _group;
        private RectTransform _timingFill;
        private RectTransform _timingRoot;

        private Team _home;
        private Team _away;

        public void Build(Team home, Team away)
        {
            _home = home;
            _away = away;

            var canvasGo = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            // Landscape reference, per §2's primary orientation.
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            // Match width and height equally, so the HUD neither shrinks to nothing
            // on a 20:9 phone nor overflows a 4:3 tablet.
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var safe = new GameObject("SafeArea", typeof(RectTransform), typeof(SafeAreaFitter));
            safe.transform.SetParent(canvasGo.transform, false);
            RectTransform safeRect = safe.GetComponent<RectTransform>();
            Stretch(safeRect);

            _group = safe.AddComponent<CanvasGroup>();
            _group.interactable = false;
            _group.blocksRaycasts = false;

            _score = Label(safeRect, "Score", 54, TextAnchor.UpperCenter,
                new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(700f, 70f));
            _homePips = PipRow(safeRect, "HomePips", new Vector2(0.5f, 1f), new Vector2(-250f, -92f));
            _awayPips = PipRow(safeRect, "AwayPips", new Vector2(0.5f, 1f), new Vector2(250f, -92f));

            _clock = Label(safeRect, "Clock", 40, TextAnchor.UpperCenter,
                new Vector2(0.5f, 1f), new Vector2(0f, -128f), new Vector2(240f, 52f));
            _prompt = Label(safeRect, "Prompt", 30, TextAnchor.LowerCenter,
                new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(900f, 44f));
            _subtitle = Label(safeRect, "Subtitle", 26, TextAnchor.LowerCenter,
                new Vector2(0.5f, 0f), new Vector2(0f, 96f), new Vector2(1100f, 40f));

            BuildTimingMeter(safeRect);
            SetPrompt(string.Empty);
            Say(string.Empty);
        }

        /// <summary>§11's zone meter: bad / good / perfect / good / bad.</summary>
        private void BuildTimingMeter(RectTransform parent)
        {
            var root = new GameObject("Timing", typeof(RectTransform), typeof(Image));
            root.transform.SetParent(parent, false);
            _timingRoot = root.GetComponent<RectTransform>();
            _timingRoot.anchorMin = _timingRoot.anchorMax = new Vector2(0.5f, 0f);
            _timingRoot.pivot = new Vector2(0.5f, 0f);
            _timingRoot.anchoredPosition = new Vector2(0f, 42f);
            _timingRoot.sizeDelta = new Vector2(620f, 26f);
            root.GetComponent<Image>().color = new Color(0.02f, 0.04f, 0.07f, 0.8f);

            // The perfect band, drawn to scale from the same constant the rules use
            // — so what the player aims at and what is scored cannot drift apart.
            var band = new GameObject("Perfect", typeof(RectTransform), typeof(Image));
            band.transform.SetParent(root.transform, false);
            RectTransform bandRect = band.GetComponent<RectTransform>();
            float half = (float)TimingWindow.Tuning.PerfectHalfWidth;
            bandRect.anchorMin = new Vector2(0.5f - half, 0f);
            bandRect.anchorMax = new Vector2(0.5f + half, 1f);
            bandRect.offsetMin = bandRect.offsetMax = Vector2.zero;
            band.GetComponent<Image>().color = new Color(1f, 0.81f, 0.24f, 0.55f);

            var good = new GameObject("Good", typeof(RectTransform), typeof(Image));
            good.transform.SetParent(root.transform, false);
            good.transform.SetAsFirstSibling();
            RectTransform goodRect = good.GetComponent<RectTransform>();
            float goodHalf = (float)TimingWindow.Tuning.GoodHalfWidth;
            goodRect.anchorMin = new Vector2(0.5f - goodHalf, 0f);
            goodRect.anchorMax = new Vector2(0.5f + goodHalf, 1f);
            goodRect.offsetMin = goodRect.offsetMax = Vector2.zero;
            good.GetComponent<Image>().color = new Color(0.16f, 0.83f, 0.96f, 0.22f);

            var marker = new GameObject("Marker", typeof(RectTransform), typeof(Image));
            marker.transform.SetParent(root.transform, false);
            _timingFill = marker.GetComponent<RectTransform>();
            _timingFill.anchorMin = new Vector2(0f, 0f);
            _timingFill.anchorMax = new Vector2(0f, 1f);
            _timingFill.pivot = new Vector2(0.5f, 0.5f);
            _timingFill.sizeDelta = new Vector2(5f, 0f);
            marker.GetComponent<Image>().color = Color.white;

            ShowTiming(false);
        }

        public void ShowTiming(bool visible)
        {
            if (_timingRoot != null) _timingRoot.gameObject.SetActive(visible);
        }

        public void SetTimingMarker(float position01)
        {
            if (_timingFill == null) return;
            _timingFill.anchorMin = new Vector2(position01, 0f);
            _timingFill.anchorMax = new Vector2(position01, 1f);
            _timingFill.anchoredPosition = Vector2.zero;
        }

        public void Refresh(MatchState match)
        {
            (int home, int away) = ShootoutRules.Scoreline(match);
            _score.text = $"{_home.ShortName}   {home} : {away}   {_away.ShortName}";
            DrawPips(_homePips, ShootoutRules.PipsFor(match, Side.Home), ToColor(_home.Neon));
            DrawPips(_awayPips, ShootoutRules.PipsFor(match, Side.Away), ToColor(_away.Neon));
        }

        public void SetClock(double? remaining)
        {
            if (_clock == null) return;
            _clock.text = remaining.HasValue ? Mathf.CeilToInt((float)remaining.Value).ToString() : string.Empty;
            // Turns amber inside the last three seconds — the point at which the
            // number stops being information and starts being pressure.
            _clock.color = remaining.HasValue && remaining.Value <= 3.0
                ? new Color(1f, 0.72f, 0.2f)
                : Color.white;
        }

        public void SetPrompt(string text) => _prompt.text = text ?? string.Empty;

        public void Say(string text) => _subtitle.text = text ?? string.Empty;

        /// <summary>Fade the HUD out while the ball is live (§27).</summary>
        public void SetDimmed(bool dimmed, float deltaTime)
        {
            if (_group == null) return;
            float target = dimmed ? 0.22f : 1f;
            _group.alpha = Mathf.MoveTowards(_group.alpha, target, deltaTime * 3.4f);
        }

        private static void DrawPips(RectTransform row, IReadOnlyList<Pip> pips, Color colour)
        {
            for (int i = row.childCount - 1; i >= 0; i--) Destroy(row.GetChild(i).gameObject);

            for (int i = 0; i < pips.Count; i++)
            {
                var pip = new GameObject($"Pip{i}", typeof(RectTransform), typeof(Image));
                pip.transform.SetParent(row, false);
                RectTransform rect = pip.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(20f, 20f);
                rect.anchoredPosition = new Vector2(i * 26f, 0f);

                // Shape as well as colour: filled, ringed, or faint.
                Image image = pip.GetComponent<Image>();
                image.color = pips[i] switch
                {
                    Pip.Goal => colour,
                    Pip.Miss => new Color(0.55f, 0.58f, 0.62f, 0.85f),
                    _ => new Color(1f, 1f, 1f, 0.14f)
                };

                if (pips[i] == Pip.Miss)
                {
                    // A hollow ring, made by insetting a background-coloured centre.
                    var hole = new GameObject("Hole", typeof(RectTransform), typeof(Image));
                    hole.transform.SetParent(pip.transform, false);
                    RectTransform holeRect = hole.GetComponent<RectTransform>();
                    holeRect.anchorMin = new Vector2(0.5f, 0.5f);
                    holeRect.anchorMax = new Vector2(0.5f, 0.5f);
                    holeRect.sizeDelta = new Vector2(11f, 11f);
                    hole.GetComponent<Image>().color = new Color(0.02f, 0.03f, 0.05f, 1f);
                }
            }
        }

        private static Color ToColor(Rgb rgb) => new Color(rgb.R / 255f, rgb.G / 255f, rgb.B / 255f);

        private static RectTransform PipRow(RectTransform parent, string name,
            Vector2 anchor, Vector2 offset)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = offset;
            rect.sizeDelta = new Vector2(160f, 24f);
            return rect;
        }

        private static Text Label(RectTransform parent, string name, int size, TextAnchor anchor,
            Vector2 anchorPoint, Vector2 offset, Vector2 size2)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = anchorPoint;
            rect.pivot = new Vector2(0.5f, anchorPoint.y);
            rect.anchoredPosition = offset;
            rect.sizeDelta = size2;

            Text text = go.GetComponent<Text>();
            text.font = BuiltinFont();
            text.fontSize = size;
            text.alignment = anchor;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static Font _font;

        /// <summary>
        /// The built-in font, whatever it is called in this Unity version.
        /// </summary>
        /// <remarks>
        /// Renamed from <c>Arial.ttf</c> to <c>LegacyRuntime.ttf</c> in Unity
        /// 2022.2. Asking for the wrong one returns null rather than throwing, and
        /// a <c>Text</c> with a null font draws nothing at all — so the failure is
        /// an invisible HUD with a clean console, which is about the worst way for
        /// this to go wrong. Both names are tried, and a missing font is reported
        /// rather than left silent.
        /// </remarks>
        private static Font BuiltinFont()
        {
            if (_font != null) return _font;

            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                    ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

            if (_font == null)
            {
                Debug.LogWarning("[CyberGoal] No built-in font found; HUD text will not render.");
            }
            return _font;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
