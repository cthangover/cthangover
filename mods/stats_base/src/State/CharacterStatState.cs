namespace Cthangover.Mods.Stats
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// What one character's stats gained during play, saved with the game: permanent
    /// bonuses, the current amount of pool stats, a changed archetype, and the level and
    /// exp of a character outside the party roster (a novel's protagonist usually is not
    /// in the battle roster, but still grows). Party members keep their level and exp in
    /// the core roster, so battles and the stats agree on one number.
    /// </summary>
    public class CharacterStatState
    {
        /// <summary>Archetype chosen during play; null keeps the template's.</summary>
        public string Archetype { get; set; }

        /// <summary>Permanent bonus per stat id.</summary>
        public Dictionary<string, double> Bonus { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Current amount per pool stat id; a missing entry means full.</summary>
        public Dictionary<string, double> Pools { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Level of a character outside the roster (0 = take the template's).</summary>
        public int Level { get; set; }

        /// <summary>Exp of a character outside the roster.</summary>
        public int Exp { get; set; }

        /// <summary>Action ids already taught by archetypes (each is taught once, even after a class change and back).</summary>
        public List<string> Learned { get; set; } = new();
    }
}
