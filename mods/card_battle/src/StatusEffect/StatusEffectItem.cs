namespace Cthangover.CardBattle.StatusEffect
{
    /// <summary>
    /// Data-only container for a status effect's serializable fields (<see cref="Type"/> and <see cref="Turns"/>).
    /// Used in <see cref="StatusEffectQueue.Copy"/> to deep-clone status effect state when a character
    /// is copied for battle. Cast to <see cref="IStatusEffect"/> when added back to a queue.
    /// </summary>
    public class StatusEffectItem
    {
        /// <summary>Effect type identifier (matches <see cref="IStatusEffect.Type"/>).</summary>
        public int Type { get; set; }
        /// <summary>Remaining turns before expiry.</summary>
        public int Turns { get; set; }

        /// <summary>
        /// Creates a field-by-field copy for deep-cloning during character duplication.
        /// </summary>
        public StatusEffectItem Copy()
        {
            return new StatusEffectItem
            {
                Type = Type,
                Turns = Turns
            };
        }
    }
}
