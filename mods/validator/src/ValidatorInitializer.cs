using Cthangover.Core.Mods;

namespace Cthangover.Validator
{
    /// <summary>
    /// Lifecycle entry point of the validator mod. <see cref="OnModResourcesReady"/>
    /// is the earliest moment when every mod has been discovered, compiled and
    /// registered, so it is the only reliable trigger for a global pass — the mod
    /// list cannot be assembled from <see cref="IModInitializer.OnModLoaded"/>,
    /// because asset-only mods (JSON/PNG without code) never produce an assembly
    /// and therefore never raise <c>ModLoadedEvent</c>.
    ///
    /// The hook runs on the main thread; all Godot-bound work (settings load,
    /// <c>user://</c> path resolution) happens inside <see cref="ValidatorService.Start"/>,
    /// and the validation itself continues on a background task so the game start
    /// is never blocked. On unload (or re-registration during an in-process
    /// reload) the pass in flight is stopped, see <see cref="ValidatorService.Stop"/>.
    /// </summary>
    public class ValidatorInitializer : IModInitializer, IModUnloadAware
    {
        /// <summary>
        /// Intentionally empty: asset-only mods are invisible to this callback, so
        /// the validator always works from the complete <c>ModRegistry</c> snapshot
        /// taken in <see cref="ValidatorService.Start"/> instead of accumulating
        /// mods here.
        /// </summary>
        public void OnModLoaded(string modId)
        {
        }

        /// <summary>
        /// Starts the background validation pass once all mods and their JSON
        /// resources are available.
        /// </summary>
        public void OnModResourcesReady()
        {
            ValidatorService.Start();
        }

        /// <summary>Stops the background pass while the validator's types are still alive.</summary>
        public void OnModUnloading()
        {
            ValidatorService.Stop();
        }

        /// <summary>Nothing to drop: the validator keeps no references to other mods between passes.</summary>
        public void OnModUnloaded(string modId)
        {
        }
    }
}
