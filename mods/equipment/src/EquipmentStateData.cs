namespace Cthangover.Mods.Equipment
{
    using System;
    using System.Collections.Generic;

    /// <summary>What everyone wears: character id → slot id → item id (the equipment slot's one blob).</summary>
    public class EquipmentStateData
    {
        /// <summary>Worn items per character and slot.</summary>
        public Dictionary<string, Dictionary<string, string>> Characters { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
