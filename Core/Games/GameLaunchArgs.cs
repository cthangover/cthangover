namespace Cthangover.Core.Games
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using Cthangover.Core.Games.LaunchParameters;
    using Cthangover.Core.Utils;
    using Godot;

    /// <summary>
    /// Central source of launch arguments. The native startup snapshot is captured once on the game thread.
    /// Current is a separate immutable snapshot replaced by game selection or the development host.
    /// </summary>
    public sealed class GameLaunchArgs
    {
        public const string GamePrefix = "--" + GameParameter.Name + "=";
        public const string LoadPrefix = "--" + LoadParameter.Name + "=";
        public const string WithModsPrefix = "--" + WithModsParameter.Name + "=";
        public const string NewFlag = "--" + NewGameParameter.Name;
        public const string AllModsFlag = "--" + AllModsParameter.Name;
        public const string FrontendFlag = "--" + FrontendParameter.Name;

        private static readonly object StartupGate = new();
        private static readonly LaunchParameter[] CoreDefinitions =
            Reflections.FindAndCreate<LaunchParameter>(typeof(GameLaunchArgs).Assembly).ToArray();
        private static readonly GameLaunchArgs Empty = Parse(Array.Empty<string>());
        private static GameLaunchArgs startup;
        private static GameLaunchArgs current;

        private readonly Dictionary<Type, LaunchParameter> parameters;

        private GameLaunchArgs(IEnumerable<string> userArgs, IReadOnlyList<string> engineArgs)
        {
            UserArgs = Array.AsReadOnly((userArgs ?? Array.Empty<string>()).ToArray());
            EngineArgs = engineArgs;

            // Long-lived snapshots retain only core types. Mod parameters are resolved on demand.
            parameters = new Dictionary<Type, LaunchParameter>(CoreDefinitions.Length);
            foreach (var definition in CoreDefinitions)
            {
                var parsed = Bind(definition);
                if (parsed.IsSpecified)
                    parameters.Add(definition.GetType(), parsed);
            }
        }

        /// <summary>Original startup arguments, unaffected by game switches and debug edits.</summary>
        public static GameLaunchArgs Startup
        {
            get
            {
                var snapshot = Volatile.Read(ref startup);
                if (snapshot != null || !GameEnvironment.IsRuntime)
                    return snapshot ?? Empty;

                lock (StartupGate)
                {
                    snapshot = startup;
                    if (snapshot == null)
                    {
                        snapshot = new GameLaunchArgs(OS.GetCmdlineUserArgs(), Array.AsReadOnly(OS.GetCmdlineArgs()));
                        Volatile.Write(ref startup, snapshot);
                    }
                    return snapshot;
                }
            }
        }

        /// <summary>The effective argument snapshot for current and subsequent consumers.</summary>
        public static GameLaunchArgs Current => Volatile.Read(ref current) ?? Startup;

        /// <summary>Captures startup arguments before background boot readers begin.</summary>
        public static void WarmUp() => _ = Startup;

        /// <summary>Parses detached user arguments without invoking native APIs.</summary>
        public static GameLaunchArgs Parse(IEnumerable<string> userArgs) => new(userArgs, Array.Empty<string>());

        /// <summary>User arguments following Godot's argument separator.</summary>
        public IReadOnlyList<string> UserArgs { get; }

        /// <summary>Original Godot engine arguments preceding the separator.</summary>
        public IReadOnlyList<string> EngineArgs { get; }

        /// <summary>Reads a built-in parameter or binds a mod's parameter class without retaining its assembly.</summary>
        public T Get<T>() where T : LaunchParameter, new()
        {
            if (parameters.TryGetValue(typeof(T), out var parameter))
                return (T)parameter;
            var definition = new T();
            return typeof(T).Assembly == typeof(GameLaunchArgs).Assembly ? definition : (T)Bind(definition);
        }

        /// <summary>Whether the argument collection contains a valid occurrence of the specified parameter.</summary>
        public bool Contains<T>() where T : LaunchParameter, new()
            => parameters.ContainsKey(typeof(T)) || (typeof(T).Assembly != typeof(GameLaunchArgs).Assembly && Get<T>().IsSpecified);

        /// <summary>Enumerates valid user occurrences in command-line order for repeatable parameters.</summary>
        public IEnumerable<T> GetAll<T>() where T : LaunchParameter, new()
        {
            var definition = new T();
            foreach (var argument in UserArgs)
                if (definition.ParseOccurrence(argument) is T { IsSpecified: true } value)
                    yield return value;
        }

        /// <summary>Checks whether any integration-test occurrence names the requested suite.</summary>
        public bool HasIntegrationTest(string name)
        {
            foreach (var parameter in GetAll<IntegrationTestParameter>())
                if (parameter.Value.Contains(name, StringComparer.Ordinal))
                    return true;
            return false;
        }

        internal LaunchParameter Bind(LaunchParameter parameter)
        {
            return FirstValidOccurrence(parameter, UserArgs)
                ?? (parameter.IncludeEngineArguments ? FirstValidOccurrence(parameter, EngineArgs) : null)
                ?? parameter;
        }

        private static LaunchParameter FirstValidOccurrence(LaunchParameter parameter, IReadOnlyList<string> arguments)
        {
            foreach (var argument in arguments)
            {
                var parsed = parameter.ParseOccurrence(argument);
                if (parsed?.IsSpecified == true)
                    return parsed;
            }
            return null;
        }

        internal bool IsAutomatedRun => parameters.Values.Any(p => p.ExcludesAutomaticHotReload);

        /// <summary>Actual boot scene named positionally in Godot's native arguments, if one was provided.</summary>
        public string EngineScenePath
        {
            get
            {
                string scene = null;
                foreach (var argument in EngineArgs)
                {
                    if (!argument.StartsWith("--", StringComparison.Ordinal)
                        && (argument.EndsWith(".tscn", StringComparison.Ordinal) || argument.EndsWith(".scn", StringComparison.Ordinal)
                            || argument.EndsWith(".escn", StringComparison.Ordinal)))
                        scene = argument;
                }
                return scene;
            }
        }

        /// <summary>All-mods wins, then a selected game, then automatic development mode or the frontend.</summary>
        public GameLaunchMode Mode => Contains<AllModsParameter>() ? GameLaunchMode.AllMods
            : Contains<GameParameter>() ? GameLaunchMode.Game
            : Contains<FrontendParameter>() ? GameLaunchMode.Frontend
            : parameters.Values.Any(p => p.RequestsDevelopmentMode && UserArgs.Any(p.MatchesKey)) ? GameLaunchMode.AllMods
            : GameLaunchMode.Frontend;

        /// <summary>Process-wide test override. Pass null to use the startup snapshot.</summary>
        public static void OverrideForTests(GameLaunchArgs args) => SetCurrent(args);

        /// <summary>Publishes the effective snapshot atomically for other readers.</summary>
        public static void SetCurrent(GameLaunchArgs args) => Volatile.Write(ref current, args);

        /// <summary>Reparses a replacement user argument list while preserving immutable native startup arguments.</summary>
        public GameLaunchArgs WithArguments(IEnumerable<string> userArgs) => new(userArgs, EngineArgs);

        /// <summary>Changes the selected game while keeping unrelated arguments and native startup data.</summary>
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
            return WithArguments(args);
        }

        private static bool IsSelectionArg(string arg)
            => arg != null && (arg.StartsWith(GamePrefix, StringComparison.Ordinal)
               || arg.StartsWith(LoadPrefix, StringComparison.Ordinal)
               || arg == NewFlag || arg == AllModsFlag || arg == FrontendFlag);

        /// <summary>Adds a mod to this process's selection without changing the saved collection.</summary>
        internal GameLaunchArgs WithAdditionalMod(string id)
        {
            var args = new List<string>(UserArgs.Where(arg => arg == null || !arg.StartsWith(WithModsPrefix, StringComparison.Ordinal)));
            args.Add(WithModsPrefix + string.Join(",", Get<WithModsParameter>().Value.Append(id).Distinct(StringComparer.OrdinalIgnoreCase)));
            return WithArguments(args);
        }

        /// <summary>User arguments to enter a specified game or save.</summary>
        public static List<string> Build(string gameId, string loadSave)
        {
            var args = new List<string> { GamePrefix + gameId };
            args.Add(string.IsNullOrEmpty(loadSave) ? NewFlag : LoadPrefix + loadSave);
            return args;
        }
    }
}
