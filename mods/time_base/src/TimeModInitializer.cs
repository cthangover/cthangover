using Cthangover.Core.Actions;
using Cthangover.Core.Mods;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Mods.Time
{
    /// <summary>
    /// Wires the time subsystem into the kernel. On
    /// <c>OnModResourcesReady</c> it installs the lighting adapters into
    /// <see cref="LightingBridge"/> (first registration wins) and registers
    /// the <see cref="TimeTickController"/> with the scene event system, so
    /// in-game time advances each timer pulse. The controller lives in a
    /// static field: <c>EnsureRegistered</c> is idempotent per instance, and
    /// a process-wide singleton guarantees a mod reload cannot stack a second
    /// timer-tick subscription onto <c>EventController</c>.
    /// </summary>
    public class TimeModInitializer : IModInitializer
    {
        private static readonly TimeTickController TickController = new();

        /// <summary>No per-mod work: all wiring happens once the whole mod set is ready.</summary>
        public void OnModLoaded(string modId)
        {
        }

        /// <summary>Installs the lighting bridge adapters and subscribes the clock tick.</summary>
        public void OnModResourcesReady()
        {
            LightingBridge.Register(LightingHooks.ClearSceneLights, LightingHooks.SetupBackgroundMaps);

            var root = (Godot.Engine.GetMainLoop() as SceneTree)?.Root;
            if (root == null)
            {
                GameLogger.Log("TIME", "TimeModInitializer: no scene tree available; time tick registration skipped", LogLevel.Warning);
                return;
            }

            TickController.EnsureRegistered(root);
        }
    }
}
