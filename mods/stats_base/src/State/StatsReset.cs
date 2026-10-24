namespace Cthangover.Mods.Stats
{
    using Cthangover.Core.Bootstrap;

    /// <summary>A new game (or the reset before a load) starts with no gained stats.</summary>
    public class StatsReset : IResettable
    {
        /// <inheritdoc />
        public string Id => "stats";

        /// <inheritdoc />
        public void ResetForNewGame() => StatState.Instance.Reset();
    }
}
