namespace Cthangover.Mods.Equipment
{
    using Cthangover.Core.Bootstrap;
    using Cthangover.Core.Mods;
    using Cthangover.Core.Scenes.Parsing;
    using Cthangover.Core.Utils;

    /// <summary>
    /// Wires equipment in: the <c>equipped()</c> condition and the new-game reset. The trait
    /// source, the item action and the scenario actions are found by their registries.
    /// The kernel removes the condition parser with this mod's assembly on unload.
    /// </summary>
    public class EquipmentModInitializer : IModInitializer
    {
        private static bool registered;

        /// <inheritdoc />
        public void OnModLoaded(string modId)
        {
            if (registered)
                return;
            registered = true;
            LeafParserRegistry.RegisterFirst(new EquippedLeafParser());
            GameLogger.Log("EQUIPMENT", "equipment: condition equipped() registered");
        }

        /// <inheritdoc />
        public void OnModResourcesReady()
        {
            AppServices.Current.RegisterResettable(new EquipmentReset());
        }
    }
}
