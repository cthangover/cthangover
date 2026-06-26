namespace Cthangover.Core.UI.Base.Lists
{
    /// <summary>
    /// Serializable margin/padding struct for list layout calculations.
    /// Marked [System.Serializable] for potential JSON serialization in mod configs.
    /// </summary>
    [System.Serializable]
    public struct Border
    {
        public float left;
        public float right;
        public float top;
        public float bottom;
    }
}
