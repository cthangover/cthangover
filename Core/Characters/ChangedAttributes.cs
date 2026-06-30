namespace Cthangover.Core.Characters
{
    /// <summary>
    /// Result struct for battle action resolution. Source and Target carry
    /// Damage/Defence snapshots for both sides of the exchange. Result indicates
    /// whether the action succeeded (e.g. hit landed vs. missed/blocked).
    /// A struct rather than a class because these are short-lived stack values
    /// created in tight battle loops — no heap allocation.
    /// </summary>
    public struct ChangedAttributes
    {
        /// <summary>
        /// Snapshot of the action source's damage and defence after the
        /// exchange.
        /// </summary>
        public CharacterSet Source;
        /// <summary>
        /// Snapshot of the action target's damage and defence after the
        /// exchange.
        /// </summary>
        public CharacterSet Target;
        /// <summary>
        /// Whether the action succeeded (e.g. hit landed vs. missed or
        /// blocked).
        /// </summary>
        public bool Result;

    }

}
