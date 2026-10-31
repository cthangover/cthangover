namespace Engine.Tests.Utils.PatchSamples
{
    using System.Collections.Generic;
    using System.Text.Json;
    using Cthangover.Core.Mods.Patches;

    /// <summary>
    /// Shaped like the v2 <c>rpg_object</c>: keyed parts and states, a list without a key, a nested
    /// component, and variants stored as written — a dictionary of layers over the object itself.
    /// </summary>
    public class PatchLayeredObjectSample
    {
        /// <summary>Entity id.</summary>
        public string Id { get; set; }

        /// <summary>Parent entity id for inheritance.</summary>
        public string Extends { get; set; }

        /// <summary>A scalar.</summary>
        public string Name { get; set; }

        /// <summary>Parts keyed by their id.</summary>
        [MergeKey("Id")]
        public List<PatchPartSample> Parts { get; set; }

        /// <summary>A list without a key.</summary>
        public List<string> Commands { get; set; }

        /// <summary>A nested component.</summary>
        public PatchInteractSample Interact { get; set; }

        /// <summary>States keyed by their id; their components replace the object's wholesale.</summary>
        [MergeKey("Id")]
        public List<PatchLayeredStateSample> States { get; set; }

        /// <summary>Partial objects by variant name, as written.</summary>
        [PatchLayer(typeof(PatchLayeredObjectSample))]
        public Dictionary<string, JsonElement> Variants { get; set; }
    }
}
