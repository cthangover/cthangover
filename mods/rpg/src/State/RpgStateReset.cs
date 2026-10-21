using Cthangover.Core.Bootstrap;

namespace Cthangover.Mods.Rpg
{
    /// <summary>New game: the player starts at the spawn of the entry map.</summary>
    public class RpgStateReset : IResettable
    {
        /// <summary>Deduplication key in <see cref="AppServices"/>.</summary>
        public string Id => "rpg";

        /// <summary>Forgets the position.</summary>
        public void ResetForNewGame() => RpgState.Instance.Reset();
    }
}
