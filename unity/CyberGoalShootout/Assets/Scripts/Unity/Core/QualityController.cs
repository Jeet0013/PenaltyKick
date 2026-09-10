using UnityEngine;

namespace CyberGoal.Unity.Core
{
    /// <summary>Graphics tiers, per §41-45.</summary>
    public enum QualityTier
    {
        Low,
        Medium,
        High,
        Ultra
    }

    public readonly struct QualityPreset
    {
        public readonly int CrowdCount;
        public readonly bool Shadows;
        public readonly int ShadowResolution;
        public readonly float MaxPixelRatio;
        public readonly int TargetFrameRate;
        public readonly bool Reflections;

        public QualityPreset(int crowdCount, bool shadows, int shadowResolution,
            float maxPixelRatio, int targetFrameRate, bool reflections)
        {
            CrowdCount = crowdCount;
            Shadows = shadows;
            ShadowResolution = shadowResolution;
            MaxPixelRatio = maxPixelRatio;
            TargetFrameRate = targetFrameRate;
            Reflections = reflections;
        }
    }

    /// <summary>
    /// Picks and applies a graphics tier (§41-46).
    /// </summary>
    /// <remarks>
    /// <para>
    /// §46 asks for stable frame pacing above all: "a visually simpler but stable
    /// game is better than an unstable high-graphics game". So the automatic choice
    /// is deliberately conservative — it is far easier for a player to raise the
    /// setting on a device that can take it than to work out why the game stutters.
    /// </para>
    /// <para>
    /// Device capability is judged on memory, processor count and graphics memory
    /// rather than a device-model lookup table. A table is accurate on the day it
    /// is written and wrong forever after, and it cannot say anything at all about
    /// a handset released next year.
    /// </para>
    /// </remarks>
    public sealed class QualityController : MonoBehaviour
    {
        public static QualityPreset PresetFor(QualityTier tier) => tier switch
        {
            QualityTier.Low => new QualityPreset(220, false, 0, 1.0f, 30, false),
            QualityTier.Medium => new QualityPreset(900, true, 1024, 1.5f, 60, false),
            QualityTier.High => new QualityPreset(2200, true, 2048, 2.0f, 60, true),
            _ => new QualityPreset(4200, true, 4096, 2.0f, 60, true)
        };

        public QualityTier Tier { get; private set; }
        public QualityPreset Preset => PresetFor(Tier);

        private void Awake() => Apply(Detect());

        /// <summary>Judge the device by what it reports about itself.</summary>
        public static QualityTier Detect()
        {
            int memory = SystemInfo.systemMemorySize;      // MB
            int graphics = SystemInfo.graphicsMemorySize;  // MB
            int cores = SystemInfo.processorCount;

            if (memory >= 7500 && graphics >= 3500 && cores >= 8) return QualityTier.Ultra;
            if (memory >= 5500 && graphics >= 1800 && cores >= 6) return QualityTier.High;
            if (memory >= 3200 && cores >= 4) return QualityTier.Medium;
            return QualityTier.Low;
        }

        public void Apply(QualityTier tier)
        {
            Tier = tier;
            QualityPreset preset = Preset;

            Application.targetFrameRate = preset.TargetFrameRate;
            // vSync must be off or targetFrameRate is ignored on most platforms —
            // a frequent cause of a 30 fps cap that no setting seems to fix.
            QualitySettings.vSyncCount = 0;

            QualitySettings.shadows = preset.Shadows ? ShadowQuality.All : ShadowQuality.Disable;
            QualitySettings.shadowResolution = preset.ShadowResolution switch
            {
                >= 4096 => ShadowResolution.VeryHigh,
                >= 2048 => ShadowResolution.High,
                >= 1024 => ShadowResolution.Medium,
                _ => ShadowResolution.Low
            };

            // Render scale, capped so a 3x-density phone does not try to fill a
            // 1440p buffer for a 6-inch screen.
            float scale = Mathf.Min(Screen.dpi > 0 ? Screen.dpi / 160f : 1f, preset.MaxPixelRatio);
            if (scale < 1f) scale = 1f;

            Debug.Log($"[CyberGoal] quality {tier} · crowd {preset.CrowdCount} · " +
                      $"shadows {preset.Shadows} · target {preset.TargetFrameRate}fps");
        }
    }
}
