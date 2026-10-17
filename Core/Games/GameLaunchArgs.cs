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
    /// Without <c>--game</c> the boot loads only the front-end (main menu, mod
    /// manager) and the player picks a game; development launches
    /// (<c>--scene=</c>, <c>--run-tests</c>, <c>--itest=</c>, test drivers) keep
    /// loading every mod, see <see cref="Mode"/>.
    /// The parse of the command line happens once per process and is cached on
    /// the main thread by <see cref="WarmUp"/>; switching games in-process
    /// replaces the cached value with <see cref="WithSelection"/> and
    /// <see cref="SetCurrent"/> (<see cref="GameSession.SwitchToAsync"/>), so
    /// every later reader (the mod-set filter, the launcher) sees the game the
    /// process runs now, not the one it was started with.
    /// </summary>
    public sealed class GameLaunchArgs
    {
        public const string GamePrefix = "--game=";
        public const string LoadPrefix = "--load=";
        public const string WithModsPrefix = "--with-mods=";
        public const string NewFlag = "--new";
        public const string AllModsFlag = "--all-mods";
        public const string FrontendFlag = "--frontend";

        /// <summary>Development/test switches that need the whole mod set when no game is named.</summary>
        private static readonly string[] DevPrefixes =
        {
            "--scene=", "--run-tests", "--itest=", "--choices=", "--timeout=", "--resource-atlas", "--locale-atlas", "--wait-for-debugger",
        };

        private static GameLaunchArgs _current;

        public string GameId { get; private init; }

        public string LoadSave { get; private init; }

        public bool NewGame { get; private init; }

        public bool AllMods { get; private init; }

        /// <summary>Forces the front-end even with development switches (menu tests).</summary>
        public bool ForceFrontend { get; private init; }

        public IReadOnlyList<string> WithMods { get; private init; } = Array.Empty<string>();

        /// <summary>A development or test switch is present.</summary>
        public bool HasDevFlag { get; private init; }

        /// <summary>The raw user args (after <c>--</c>), for gates parsed off the main thread (test suites).</summary>
        public IReadOnlyList<string> UserArgs { get; private init; } = Array.Empty<string>();

        /// <summary>
        /// What the boot loads: <c>--all-mods</c> wins, then <c>--game</c>, then a
        /// development switch (all mods), otherwise the front-end alone.
        /// </summary>
        public GameLaunchMode Mode => AllMods ? GameLaunchMode.AllMods
            : !string.IsNullOrEmpty(GameId) ? GameLaunchMode.Game
            : ForceFrontend ? GameLaunchMode.Frontend
            : HasDevFlag ? GameLaunchMode.AllMods
            : GameLaunchMode.Frontend;

        /// <summary>True when the boot should load a single game.</summary>
        public bool SelectsGame => Mode == GameLaunchMode.Game;

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
                ForceFrontend = args.Contains(FrontendFlag),
                HasDevFlag = args.Any(a => DevPrefixes.Any(p => a.StartsWith(p, StringComparison.Ordinal))),
                UserArgs = args,
                WithMods = (Value(WithModsPrefix) ?? string.Empty)
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            };
        }

        /// <summary>Process-wide override for tests that need a specific mode without a command line.</summary>
        public static void OverrideForTests(GameLaunchArgs args) => _current = args;

        /// <summary>Makes <paramref name="args"/> the selection of this process (an in-process game switch).</summary>
        public static void SetCurrent(GameLaunchArgs args) => _current = args;

        /// <summary>
        /// The same launch with another selection: <paramref name="gameId"/>
        /// started fresh or from <paramref name="loadSave"/>, or the front-end
        /// when <paramref name="gameId"/> is null. Development and test switches
        /// (<c>--itest=</c>, <c>--with-mods=</c>, …) are kept, the previous
        /// selection (<c>--game</c>, <c>--new</c>, <c>--load</c>, <c>--all-mods</c>,
        /// <c>--frontend</c>) is replaced.
        /// </summary>
        public GameLaunchArgs WithSelection(string gameId, string loadSave = null)
        {
            var args = new List<string>();
            foreach (var arg in UserArgs)
            {
                if (!IsSelectionArg(arg))
                    args.Add(arg);
            }
            if (string.IsNullOrEmpty(gameId))
                args.Add(FrontendFlag);
            else
                args.AddRange(Build(gameId, loadSave));
            return Parse(args);
        }

        private static bool IsSelectionArg(string arg)
            => arg.StartsWith(GamePrefix, StringComparison.Ordinal)
               || arg.StartsWith(LoadPrefix, StringComparison.Ordinal)
               || arg == NewFlag || arg == AllModsFlag || arg == FrontendFlag;

        /// <summary>User args that start <paramref name="gameId"/> fresh or from <paramref name="loadSave"/>.</summary>
        public static List<string> Build(string gameId, string loadSave)
        {
            var args = new List<string> { GamePrefix + gameId };
            args.Add(string.IsNullOrEmpty(loadSave) ? NewFlag : LoadPrefix + loadSave);
            return args;
        }
    }
}
