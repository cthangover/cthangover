namespace Engine.Tests.Utils.PatchSamples
{
    /// <summary>Per-cell override identified by layer and position together.</summary>
    public class PatchCellSample
    {
        /// <summary>Layer name.</summary>
        public string Layer { get; set; }

        /// <summary>Column.</summary>
        public int X { get; set; }

        /// <summary>Row.</summary>
        public int Y { get; set; }

        /// <summary>The overridden value.</summary>
        public string Order { get; set; }
    }
}
