namespace Engine.Tests.Utils.PatchSamples
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Cthangover.Core.Mods.Patches;

    /// <summary>
    /// Object-shaped DTO of the v2 model: keyed lists declared with <see cref="MergeKeyAttribute"/>
    /// (single and composite), a list without a key, and a dictionary of fragments of its own type.
    /// </summary>
    public class PatchObjectSample
    {
        /// <summary>Entity id.</summary>
        public string Id { get; set; }

        /// <summary>Parent entity id for inheritance.</summary>
        public string Extends { get; set; }

        /// <summary>A scalar inherited unless the child sets it.</summary>
        public string Name { get; set; }

        /// <summary>Parts keyed by their id.</summary>
        [MergeKey("Id")]
        public List<PatchPartSample> Parts { get; set; }

        /// <summary>States keyed by their id; each has its own keyed parts.</summary>
        [MergeKey("Id")]
        public List<PatchStateSample> States { get; set; }

        /// <summary>Cells keyed by layer and position (composite key), JSON name <c>Overrides</c>.</summary>
        [MergeKey("Layer", "X", "Y")]
        [JsonPropertyName("Overrides")]
        public List<PatchCellSample> Cells { get; set; }

        /// <summary>Commands: a list without a key.</summary>
        public List<string> Commands { get; set; }

        /// <summary>Named fragments of the same shape (like map variants).</summary>
        public Dictionary<string, PatchObjectSample> Variants { get; set; }
    }
}
