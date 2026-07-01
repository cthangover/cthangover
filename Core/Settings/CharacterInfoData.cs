using System.Collections.Generic;
using Cthangover.Core.Characters;

namespace Cthangover.Core.Settings
{
    /// <summary>
    /// Serialization-safe snapshot of a recruited character's mutable
    /// runtime state. Stored inside <see cref="RuntimeData.CharacterData"/>
    /// keyed by <see cref="CharacterType"/>, and serialized to
    /// <see cref="SaveData.Characters"/> on save. Does not hold the full
    /// <see cref="Cthangover.Core.Characters.Character"/> blueprint — that
    /// is fetched from <see cref="Cthangover.Core.Factories.Impls.CharacterFactory"/>
    /// via <see cref="ID"/>.
    /// </summary>
    public class CharacterInfoData
    {
        /// <summary>Factory key identifying the character blueprint.</summary>
        public string         ID { get; set; }
        /// <summary>Type discriminator used as dictionary key in <see cref="CharacterData"/>.</summary>
        public string         CharacterType { get; set; }
        /// <summary>Current experience level.</summary>
        public int            Level { get; set; }
        /// <summary>Accumulated experience points toward next level.</summary>
        public int            Exp { get; set; }
        /// <summary>Full set of battle attributes (health, mana, etc.).</summary>
        public CharacterAttributes Attributes { get; set; }
        /// <summary>
        /// Up to 3 action IDs assigned to this character's slots.
        /// Index 0,1,2 map to slot positions; empty slots hold <c>null</c>.
        /// When <c>null</c> (<see cref="System.Text.Json"/> default for missing keys
        /// in older saves), the battle system falls back to the factory template.
        /// </summary>
        public List<string> ActionSlots { get; set; }
    }

}
