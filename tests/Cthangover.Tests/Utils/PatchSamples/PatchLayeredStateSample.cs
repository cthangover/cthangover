namespace Engine.Tests.Utils.PatchSamples
{
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Cthangover.Core.Mods.Patches;

    /// <summary>State of a <see cref="PatchLayeredObjectSample"/>: every undeclared property is a component that replaces the object's one wholesale.</summary>
    public class PatchLayeredStateSample
    {
        /// <summary>Identity inside the object.</summary>
        public string Id { get; set; }

        /// <summary>Condition expression.</summary>
        public string When { get; set; }

        /// <summary>The components as written.</summary>
        [JsonExtensionData]
        [PatchLayer(typeof(PatchLayeredObjectSample), Wholesale = true)]
        public Dictionary<string, JsonElement> Components { get; set; }
    }
}
