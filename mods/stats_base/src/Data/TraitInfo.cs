namespace Cthangover.Mods.Stats
{
    /// <summary>
    /// A modifier of one stat, carried by anything: an archetype, a character template, an
    /// equipped item, a status, a passive skill — any entity may hold a <c>"Traits"</c> list
    /// in its JSON, and any mod may contribute traits through an <see cref="ITraitSource"/>.
    /// A stat's final value is <c>(value + Σ Add + Σ Formula) × (1 + Σ Percent / 100)</c>.
    /// <para/>
    /// Traits are the reason gear and statuses need no code of their own: a sword is an
    /// item with <c>{"Stat": "atk", "Add": 5}</c>, a blessing is a trait of a status.
    /// </summary>
    public class TraitInfo
    {
        /// <summary>Stat id the trait changes.</summary>
        public string Stat { get; set; }

        /// <summary>Flat amount added to the stat.</summary>
        public double Add { get; set; }

        /// <summary>Percent the stat grows by (negative shrinks), summed over all traits of the stat.</summary>
        public double Percent { get; set; }

        /// <summary>Formula of a flat amount, evaluated against the owner's other stats (<c>"level * 2"</c>).</summary>
        public string Formula { get; set; }
    }
}
