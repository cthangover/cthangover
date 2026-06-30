namespace Cthangover.Core.Characters
{
    /// <summary>
    /// Lightweight snapshot of damage/defence values for battle action results.
    /// A struct to avoid heap allocation in the battle loop where these are
    /// created for every action exchange. Used by ChangedAttributes to carry
    /// both source and target state.
    /// </summary>
    public struct CharacterSet
    {
        /// <summary>
        /// Damage dealt or received in this exchange.
        /// </summary>
        public int Damage;
        /// <summary>
        /// Defence value at the time of the exchange.
        /// </summary>
        public int Defence;
    }
}
