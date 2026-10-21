using Cthangover.Core.Bootstrap;
using Cthangover.Core.Events;
using Cthangover.Core.Mods;

namespace Cthangover.Mods.Rpg
{
    /// <summary>
    /// Wires the rpg mod into the session: the new-game reset of the position
    /// and the battle-end hook for win flags. The world itself is not created
    /// here — <see cref="RpgWorldHost"/> sits in the shell scene (scene patch)
    /// and mounts a map whenever a map scene is entered. Save data goes through
    /// <see cref="RpgSaveable"/>, found by the kernel automatically.
    /// </summary>
    public class RpgModInitializer : IModInitializer
    {
        private static bool subscribed;

        /// <summary>Nothing per mod.</summary>
        public void OnModLoaded(string modId)
        {
        }

        /// <summary>Registers the reset (deduplicated by id) and subscribes once.</summary>
        public void OnModResourcesReady()
        {
            AppServices.Current.RegisterResettable(new RpgStateReset());
            if (subscribed)
                return;
            subscribed = true;
            ModEventBus.Subscribe<BattleEndedEvent>(RpgBattleService.OnBattleEnded);
        }
    }
}
