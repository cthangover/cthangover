using Cthangover.Core.Games;

namespace Cthangover.Mods.Rpg
{
    /// <summary>
    /// The player's map sprite is a choice of the game, not of a map: it comes
    /// from the collection setting <c>rpg.player_charset</c> (a path in the
    /// <c>charsets</c> group), so two games can share the maps of a pack with
    /// different heroes.
    /// </summary>
    public static class RpgPlayerLook
    {
        /// <summary>Collection setting key.</summary>
        public const string SettingKey = "rpg.player_charset";

        private const string Fallback = "hero";

        /// <summary>The configured charset path, or <c>hero</c>.</summary>
        public static string Charset()
        {
            var value = ActiveGame.GetSetting(SettingKey);
            return string.IsNullOrWhiteSpace(value) ? Fallback : value;
        }
    }
}
