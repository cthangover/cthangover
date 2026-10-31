namespace Engine.Tests.Utils.PatchSamples
{
    /// <summary>Nested light object; <see cref="Color"/> has a non-empty default so a lost value is visible.</summary>
    public class PatchLightSample
    {
        /// <summary>Radius in cells.</summary>
        public int Radius { get; set; } = 3;

        /// <summary>Light colour.</summary>
        public string Color { get; set; } = "#ffd8a0";
    }
}
