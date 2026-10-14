using System;

namespace Cthangover.Core.Bootstrap
{
    /// <summary>
    /// Progress source for the launcher boot overlay. The boot pipeline and the
    /// mod compiler report coarse phase entries and fine-grained sub-steps here;
    /// the launcher UI reads a thread-safe snapshot once per frame and maps it
    /// onto a progress bar.
    ///
    /// Weights live here rather than in the UI so the progress model has a single
    /// owner: Discovering dominates real boot time (manifest scanning, hashing,
    /// Roslyn compilation), while the Godot-bound phases are comparatively cheap.
    /// Values were sampled from GameLogger timestamps on a cold-cache run and are
    /// hardcoded on purpose — a config entry would be read before paths exist.
    /// </summary>
    public static class BootProgress
    {
        private static readonly object Sync = new();

        private static BootPhase _phase = BootPhase.NotStarted;
        private static float _sub;

        /// <summary>
        /// Marks the start of a pipeline phase. Called from the boot thread(s);
        /// sub-progress resets because sub-steps never span phases.
        /// </summary>
        public static void EnterPhase(BootPhase phase)
        {
            lock (Sync)
            {
                _phase = phase;
                _sub = 0f;
            }
        }

        /// <summary>Reports sub-progress (0..1) inside the current phase.</summary>
        public static void ReportSub(float sub)
        {
            sub = Math.Clamp(sub, 0f, 1f);
            lock (Sync)
            {
                if (sub > _sub)
                    _sub = sub;
            }
        }

        /// <summary>
        /// Snapshot of overall boot progress (0..1). Weighted by the phase
        /// weights below; phases after the current one contribute nothing.
        /// </summary>
        public static float GetOverall()
        {
            lock (Sync)
            {
                return OverallFor(_phase, _sub);
            }
        }

        private static readonly int WeightPreparingPaths = 5;
        private static readonly int WeightDiscovering = 60;
        private static readonly int WeightFinalizingMods = 15;
        private static readonly int WeightPostMods = 20;
        private const int TotalWeight = 100;

        private static float OverallFor(BootPhase phase, float sub)
        {
            switch (phase)
            {
                case BootPhase.Ready:
                    return 1f;
                case BootPhase.PreparingPaths:
                    return WeightPreparingPaths * sub / TotalWeight;
                case BootPhase.Discovering:
                    return (WeightPreparingPaths + WeightDiscovering * sub) / TotalWeight;
                case BootPhase.FinalizingMods:
                    return (WeightPreparingPaths + WeightDiscovering + WeightFinalizingMods * sub) / TotalWeight;
                case BootPhase.ModsReady:
                    return (WeightPreparingPaths + WeightDiscovering + WeightFinalizingMods) / TotalWeight;
                case BootPhase.Locale:
                    return (WeightPreparingPaths + WeightDiscovering + WeightFinalizingMods + WeightPostMods / 3f) / TotalWeight;
                case BootPhase.Scenes:
                    return (WeightPreparingPaths + WeightDiscovering + WeightFinalizingMods + WeightPostMods * 2f / 3f) / TotalWeight;
                default:
                    return 0f;
            }
        }
    }
}
