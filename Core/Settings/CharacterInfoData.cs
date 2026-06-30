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
    }

}
