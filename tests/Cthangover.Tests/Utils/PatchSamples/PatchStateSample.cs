namespace Engine.Tests.Utils.PatchSamples
{
    using System.Collections.Generic;
    using Cthangover.Core.Mods.Patches;

    /// <summary>State of an object: a condition and its own keyed parts (a key one level deeper).</summary>
    public class PatchStateSample
    {
        /// <summary>Identity inside the object.</summary>
        public string Id { get; set; }

        /// <summary>Condition expression.</summary>
        public string When { get; set; }

        /// <summary>Parts keyed by their id.</summary>
        [MergeKey("Id")]
        public List<PatchPartSample> Parts { get; set; }
    }
}
