namespace Engine.Tests.Utils.PatchSamples
{
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Cthangover.Core.Mods.Patches;

    /// <summary>Shaped like <c>rpg_placement</c>: declared fields plus per-instance overrides of the object, deep-merged over it.</summary>
    public class PatchPlacementSample
    {
        /// <summary>Placement id.</summary>
        public string Id { get; set; }

        /// <summary>Placed object id.</summary>
        public string Object { get; set; }

        /// <summary>Column.</summary>
        public int X { get; set; }

        /// <summary>Row.</summary>
        public int Y { get; set; }

        /// <summary>Overridden object fields as written.</summary>
        [JsonExtensionData]
        [PatchLayer(typeof(PatchLayeredObjectSample))]
        public Dictionary<string, JsonElement> Overrides { get; set; }
    }
}
