namespace Cthangover.Mods.Crafting
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Cthangover.Core.Factories;

    /// <summary>
    /// A station offers an explicit set of existing recipe entities. The same definition
    /// can be opened from a novel choice, an interactive or a map event; no scene is implied.
    /// </summary>
    [EntityKind("craft_station")]
    public class CraftStation : IIdentifiable
    {
        /// <summary>Station id used by craft.open.</summary>
        [JsonPropertyName("Id")]
        public string ID { get; set; }

        /// <summary>Localized window title.</summary>
        public string Name { get; set; }

        /// <summary>Recipe ids offered in order; unknown recipes are omitted by the window.</summary>
        public List<string> Recipes { get; set; } = new();
    }
}
