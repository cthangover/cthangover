using System.Linq;

namespace Cthangover.Core.Characters
{
    /// <summary>
    /// Extension for querying passive effects on a <see cref="Character"/>.
    /// Passive actions are <see cref="ActionCharacter"/> entries with
    /// <see cref="ActionCharacterType.Passive"/> whose <see cref="ActionCharacter.Properties"/>
    /// declare effect flags as boolean keys (e.g. "IgnoreArmor"). The key names
    /// are chosen by the mod author — the core imposes no registry or constants.
    /// </summary>
    public static class CharacterPassiveExtension
    {
        /// <summary>
        /// Returns <c>true</c> if the character has at least one Passive action
        /// whose <see cref="ActionCharacter.Properties"/> contains <paramref name="key"/>
        /// with a <c>true</c> boolean value.
        /// </summary>
        public static bool HasPassiveEffect(this Character character, string key)
        {
            return character?.Actions?.Any(a =>
                a.Type == ActionCharacterType.Passive && a.GetBool(key)) ?? false;
        }
    }
}
