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
        public CharacterSet Source;
        public CharacterSet Target;
        public bool Result;

    }

}
