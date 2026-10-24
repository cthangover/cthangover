namespace Cthangover.Mods.Stats
{
    using System.Globalization;

    /// <summary>Number arguments of the stats actions: invariant culture, a sign allowed (<c>value=-1</c>).</summary>
    public static class StatArgs
    {
        /// <summary>The number in <paramref name="text"/>, or <paramref name="fallback"/> when it is empty or not a number.</summary>
        public static double Number(string text, double fallback = 0)
            => double.TryParse(text?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : fallback;
    }
}
