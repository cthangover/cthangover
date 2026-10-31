namespace Engine.Tests.Utils.PatchSamples
{
    using System.Collections.Generic;

    /// <summary>Interaction component of <see cref="PatchLayeredObjectSample"/>: a nested object with a list without a key.</summary>
    public class PatchInteractSample
    {
        /// <summary>Scenario path.</summary>
        public string Scenario { get; set; }

        /// <summary>Inline lines, a list without a key.</summary>
        public List<string> Commands { get; set; }

        /// <summary>Target map of a teleport.</summary>
        public string Teleport { get; set; }
    }
}
