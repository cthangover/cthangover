namespace Cthangover.Mods.Stats
{
    using Cthangover.Core.Bootstrap;
    using Cthangover.Core.Events;
    using Cthangover.Core.Mods;
    using Cthangover.Core.Scenes.Parsing;
    using Cthangover.Core.Utils;

    /// <summary>
    /// Wires the stats into the engine: the <c>stat()</c>/<c>level()</c>/<c>archetype()</c>
    /// conditions, the new-game reset and the roster sync on scene entry and dialog end.
    /// <para/>
    /// The condition parser goes first in the registry; the kernel removes it with this
    /// mod's assembly on unload.
    /// </summary>
    public class StatsModInitializer : IModInitializer
    {
        private static bool registered;

        /// <inheritdoc />
        public void OnModLoaded(string modId)
        {
            if (registered)
                return;
            registered = true;
            LeafParserRegistry.RegisterFirst(new StatLeafParser());
            ModEventBus.Subscribe<SceneEnteredEvent>(StatSync.OnSceneEntered);
            ModEventBus.Subscribe<DialogEndedEvent>(StatSync.OnDialogEnded);
            GameLogger.Log("STATS", "stats: conditions stat()/level()/archetype() and the roster sync registered");
        }

        /// <inheritdoc />
        public void OnModResourcesReady()
        {
            AppServices.Current.RegisterResettable(new StatsReset());
            StatsService.ClearCache();
        }
    }
}
