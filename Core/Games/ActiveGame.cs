using System;
using System.Collections.Generic;
using Cthangover.Core.Mods;

namespace Cthangover.Core.Games
{
    /// <summary>
    /// The game this process runs, chosen at boot from <c>--game=&lt;id&gt;</c>.
    /// Empty when the process loads every mod (<c>--all-mods</c>, development
    /// launches and tests) — callers then keep the pre-collection behaviour
    /// (<see cref="EntryScene"/> falls back to <c>start_scene</c>, saves go to
    /// the shared folder). The id is the save identity of the game: it stays
    /// stable while the player edits the collection's mechanics.
    /// </summary>
    public static class ActiveGame
    {
        /// <summary>Entry scene used without an active game or when the collection declares none.</summary>
        public const string DefaultEntryScene = "start_scene";

        private static GameModSet _set;

        public static bool IsActive => _set?.Game != null;

        /// <summary>True when the process booted only the front-end (no game chosen yet).</summary>
        public static bool IsFrontendOnly { get; private set; }

        public static string Id => _set?.Game?.Id;

        public static ModCollection Collection => _set?.Game;

        /// <summary>Mods loaded for the game; empty when no game is active.</summary>
        public static IReadOnlyCollection<string> ModIds => (IReadOnlyCollection<string>)_set?.ModIds ?? Array.Empty<string>();

        public static string EntryScene => string.IsNullOrWhiteSpace(_set?.Game?.Entry?.Scene)
            ? DefaultEntryScene
            : _set.Game.Entry.Scene;

        /// <summary>Game parameter from the collection's <c>settings</c>, or null.</summary>
        public static string GetSetting(string key)
            => key != null && _set?.Game?.Settings != null && _set.Game.Settings.TryGetValue(key, out var value)
                ? value
                : null;

        /// <summary>Called by the boot pipeline once the game's mod set is applied.</summary>
        public static void Activate(GameModSet set)
        {
            _set = set;
            IsFrontendOnly = false;
        }

        /// <summary>Called by the boot pipeline when it kept only the front-end.</summary>
        public static void ActivateFrontend(GameModSet set)
        {
            _set = set;
            IsFrontendOnly = true;
        }

        public static void Clear()
        {
            _set = null;
            IsFrontendOnly = false;
        }
    }
}
