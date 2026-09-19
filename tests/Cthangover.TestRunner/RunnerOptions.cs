namespace Cthangover.TestRunner
{
    /// <summary>
    /// Parsed command-line options of the global test runner. Kept as a plain
    /// class with a hand-written parser: the runner must build and run with
    /// nothing but the .NET 8 SDK, so no CLI framework dependency is added.
    /// </summary>
    public class RunnerOptions
    {
        /// <summary>Run only this test (unit or an integration name from the manifest).</summary>
        public string Test { get; private set; }

        /// <summary>Print available tests and exit.</summary>
        public bool List { get; private set; }

        /// <summary>Explicit path to the Godot executable; falls back to GODOT_BIN or PATH.</summary>
        public string Godot { get; private set; }

        /// <summary>Skip the game assembly build before integration tests.</summary>
        public bool NoBuild { get; private set; }

        /// <summary>Write a machine-readable JSON report to this path.</summary>
        public string Json { get; private set; }

        /// <summary>Run SceneSwitcher against this scene path or logical scene name.</summary>
        public string Switcher { get; private set; }

        /// <summary>Comma-separated dialog choice indexes for --switcher.</summary>
        public string Choices { get; private set; }

        /// <summary>SceneSwitcher timeout in seconds.</summary>
        public int TimeoutSeconds { get; private set; } = 60;

        /// <summary>Print usage and exit.</summary>
        public bool ShowHelp { get; private set; }

        public static RunnerOptions Parse(string[] args)
        {
            var options = new RunnerOptions();
            for (var i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--test": options.Test = Next(args, ref i); break;
                    case "--list": options.List = true; break;
                    case "--godot": options.Godot = Next(args, ref i); break;
                    case "--no-build": options.NoBuild = true; break;
                    case "--json": options.Json = Next(args, ref i); break;
                    case "--switcher": options.Switcher = Next(args, ref i); break;
                    case "--choices": options.Choices = Next(args, ref i); break;
                    case "--timeout":
                        if (!int.TryParse(Next(args, ref i), out var timeout) || timeout <= 0)
                            throw new ArgumentException("--timeout expects a positive number of seconds");
                        options.TimeoutSeconds = timeout;
                        break;
                    case "--help":
                    case "-h":
                        options.ShowHelp = true;
                        break;
                    default:
                        throw new ArgumentException($"unknown option '{args[i]}'");
                }
            }
            return options;
        }

        private static string Next(string[] args, ref int i)
        {
            if (i + 1 >= args.Length)
                throw new ArgumentException($"option '{args[i]}' requires a value");
            return args[++i];
        }

        public static string HelpText =>
@"CThangover — глобальный раннер тестов (кроссплатформенный, .NET 8 + Godot .NET).

Использование:
  dotnet run --project tests/Cthangover.TestRunner -- [опции]

Без опций: сборка игры → unit-тесты → все интеграционные сцены из
tests/tests.manifest.json → сводка. Код выхода 0 только если всё зелёное.

Опции:
  --test <name>        выполнить только один тест: unit или имя из манифеста
                       (mod, actioncontext, tscn, scene_builder, coverage,
                        battle, battle_ff, menu)
  --list               показать список тестов
  --godot <path>       путь к Godot (по умолчанию GODOT_BIN или godot из PATH)
  --no-build           не собирать Cthangover.csproj перед интеграционными тестами
  --json <path>        записать машинный отчёт (JSON)
  --switcher <target>  smoke-прогон сцены через SceneSwitcher (путь .tscn или
                       логическое имя), с авто-диалогами
  --choices <0,1,...>  индексы выборов для --switcher
  --timeout <sec>      таймаут SceneSwitcher (по умолчанию 60)
  --help               эта справка

Примеры:
  dotnet run --project tests/Cthangover.TestRunner
  dotnet run --project tests/Cthangover.TestRunner -- --test battle_ff
  dotnet run --project tests/Cthangover.TestRunner -- --switcher home_kitchen --choices 0 --timeout 30
  dotnet run --project tests/Cthangover.TestRunner -- --json test-results.json";
    }
}
