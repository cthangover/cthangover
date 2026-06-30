namespace Cthangover.Core.UI.Base.Lists
{
    /// <summary>
    /// Serializable margin/padding struct for list layout calculations.
    /// Marked [System.Serializable] for potential JSON serialization in mod configs.
    /// </summary>
    [System.Serializable]
    public struct Border
    {
        /// <summary>Left margin or padding, in pixels.</summary>
        public float left;
        /// <summary>Right margin or padding, in pixels.</summary>
        public float right;
        /// <summary>Top margin or padding, in pixels.</summary>
        public float top;
        /// <summary>Bottom margin or padding, in pixels.</summary>
        public float bottom;
    }
}
