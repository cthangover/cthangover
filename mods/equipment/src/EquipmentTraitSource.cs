namespace Cthangover.Mods.Equipment
{
    using System.Collections.Generic;
    using Cthangover.Mods.Stats;

    /// <summary>The traits of everything a character wears: each worn item's <c>"Traits"</c> list.</summary>
    public class EquipmentTraitSource : ITraitSource
    {
        /// <inheritdoc />
        public string Id => "equipment";

        /// <inheritdoc />
        public IEnumerable<TraitInfo> TraitsOf(string characterId)
        {
            foreach (var item in EquipmentService.Worn(characterId).Values)
            {
                foreach (var trait in ExtraData.Traits(EquipmentService.Item(item)?.ExtraProperties, item))
                    yield return trait;
            }
        }
    }
}
