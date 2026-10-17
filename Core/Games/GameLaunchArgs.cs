using System;
using System.Collections.Generic;
using System.Linq;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Games
{
    /// <summary>
    /// User args (after <c>--</c>) that pick what the boot loads and where the
    /// launcher goes next:
    /// <list type="bullet">
    /// <item><c>--game=&lt;collection id&gt;</c> — load only that game's mods
    /// (plus the front-end) and enter it;</item>
    /// <item><c>--new</c> — start a new game (default when no save is given);</item>
    /// <item><c>--load=&lt;save&gt;</c> — load a save of that game instead;</item>
    /// <item><c>--with-mods=a,b</c> — extra mods on top of the game (tests, debugging);</item>
    /// <item><c>--all-mods</c> — ignore <c>--game</c> and load every installed mod.</item>
    /// </list>
    /// Switching games restarts the process with a new set of these args
    /// (<see cref="GameSession.RestartInto"/>), so the parse happens once per
    /// process and is cached on the main thread by <see cref="WarmUp"/>.
    /// </summary>
    public sealed class GameLaunchArgs
    {
        public const string GamePrefix = "--game=";
        public const string LoadPrefix = "--load=";
        public const string WithModsPrefix = "--with-mods=";
        public const string NewFlag = "--new";
        public const string AllModsFlag = "--all-mods";

        private static GameLaunchArgs _current;

        public string GameId { get; private init; }

        public string LoadSave { get; private init; }

        public bool NewGame { get; private init; }

        public bool AllMods { get; private init; }

        public IReadOnlyList<string> WithMods { get; private init; } = Array.Empty<string>();

        /// <summary>True when the boot should load a single game.</summary>
        public bool SelectsGame => !AllMods && !string.IsNullOrEmpty(GameId);

        /// <summary>Process-wide args; parsed lazily, or eagerly on the main thread by <see cref="WarmUp"/>.</summary>
        public static GameLaunchArgs Current => _current ??= GameEnvironment.IsRuntime
            ? Parse(OS.GetCmdlineUserArgs())
            : new GameLaunchArgs();

        /// <summary>Parses on the main thread before the background boot phases read <see cref="Current"/>.</summary>
        public static void WarmUp() => _ = Current;

        public static GameLaunchArgs Parse(IEnumerable<string> userArgs)
        {
            var args = (userArgs ?? Array.Empty<string>()).ToList();
            string Value(string prefix) => args
                .Where(a => a.StartsWith(prefix, StringComparison.Ordinal))
                .Select(a => a.Substring(prefix.Length).Trim())
                .FirstOrDefault(v => v.Length > 0);

            return new GameLaunchArgs
            {
                GameId = Value(GamePrefix),
                LoadSave = Value(LoadPrefix),
                NewGame = args.Contains(NewFlag),
                AllMods = args.Contains(AllModsFlag),
                WithMods = (Value(WithModsPrefix) ?? string.Empty)
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            };
        }

        /// <summary>User args that start <paramref name="gameId"/> fresh or from <paramref name="loadSave"/>.</summary>
        public static List<string> Build(string gameId, string loadSave)
        {
            var args = new List<string> { GamePrefix + gameId };
            args.Add(string.IsNullOrEmpty(loadSave) ? NewFlag : LoadPrefix + loadSave);
            return args;
        }
    }
}
