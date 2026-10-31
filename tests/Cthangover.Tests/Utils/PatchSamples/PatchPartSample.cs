namespace Engine.Tests.Utils.PatchSamples
{
    /// <summary>Picture part of an object, identified by <see cref="Id"/>.</summary>
    public class PatchPartSample
    {
        /// <summary>Identity inside the object.</summary>
        public string Id { get; set; }

        /// <summary>Picture path.</summary>
        public string Image { get; set; }

        /// <summary>Offset kept by partial patches.</summary>
        public int X { get; set; }
    }
}
