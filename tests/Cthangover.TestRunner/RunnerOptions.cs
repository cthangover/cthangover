namespace Cthangover.TestRunner
{
    /// <summary>Selection is separate from execution; --plan and --list never build or start a game.</summary>
    public class RunnerOptions
    {
        public List<string> Tests { get; } = new();
        public string Mod { get; private set; }
        public string Filter { get; private set; }
        public string Profile { get; private set; }
        public string Tag { get; private set; }
        public string Kind { get; private set; }
        public string Game { get; private set; }
        public string Renderer { get; private set; }
        public string Failed { get; private set; }
        public string Catalog { get; private set; }
        public string Artifacts { get; private set; }
        public bool Plan { get; private set; }
        public bool List { get; private set; }
        public string Godot { get; private set; }
        public bool NoBuild { get; private set; }
        /// <summary>Uses a disposable per-run compilation cache instead of the persistent cache.</summary>
        public bool FreshCache { get; private set; }
        public string Json { get; private set; }
        public string Switcher { get; private set; }
        public string Choices { get; private set; }
        public int TimeoutSeconds { get; private set; } = 60;
        public bool ShowHelp { get; private set; }
        public bool KeepWorkspace { get; private set; }
        /// <summary>Inactive logical runs expire by age, count or total bytes; CLI overrides the corresponding CTHANGOVER_TEST_RETENTION_* environment values.</summary>
        public double RetentionDays { get; private set; } = 3;
        public int RetentionCount { get; private set; } = 3;
        public long RetentionBytes { get; private set; } = 20L * 1024 * 1024 * 1024;

        /// <summary>Global child limit; one explicitly selects the original sequential workspace protocol.</summary>
        public int Jobs { get; private set; } = Math.Clamp(Environment.ProcessorCount / 2, 2, 8);
        /// <summary>Windowed children also count against the global limit.</summary>
        public int GuiJobs { get; private set; } = 2;
        /// <summary>Maximum exclusive project copies; dependency components hold a copy until complete.</summary>
        public int WriteJobs { get; private set; } = 2;

        public static RunnerOptions Parse(string[] args)
        {
            var options = new RunnerOptions();
            options.RetentionDays = Nonnegative(Environment.GetEnvironmentVariable("CTHANGOVER_TEST_RETENTION_DAYS") ?? "3", "CTHANGOVER_TEST_RETENTION_DAYS");
            options.RetentionCount = RetainedCount(Environment.GetEnvironmentVariable("CTHANGOVER_TEST_RETENTION_COUNT") ?? "3");
            options.RetentionBytes = RetainedBytes(Environment.GetEnvironmentVariable("CTHANGOVER_TEST_RETENTION_GIB") ?? "20");
            for (var i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--test": options.Tests.Add(Next(args, ref i)); break;
                    case "--mod": options.Mod = Next(args, ref i); break;
                    case "--filter": options.Filter = Next(args, ref i); break;
                    case "--profile": options.Profile = Next(args, ref i); break;
                    case "--tag": options.Tag = Next(args, ref i); break;
                    case "--kind": options.Kind = Next(args, ref i); break;
                    case "--game": options.Game = Next(args, ref i); break;
                    case "--renderer": options.Renderer = Next(args, ref i); break;
                    case "--failed": options.Failed = Next(args, ref i); break;
                    case "--catalog": options.Catalog = Next(args, ref i); break;
                    case "--artifacts": options.Artifacts = Next(args, ref i); break;
                    case "--plan": options.Plan = true; break;
                    case "--list": options.List = true; break;
                    case "--godot": options.Godot = Next(args, ref i); break;
                    case "--no-build": options.NoBuild = true; break;
                    case "--fresh-cache": options.FreshCache = true; break;
                    case "--keep-workspace": options.KeepWorkspace = true; break;
                    case "--retention-days": options.RetentionDays = Nonnegative(Next(args, ref i), "--retention-days"); break;
                    case "--retention-count": options.RetentionCount = RetainedCount(Next(args, ref i)); break;
                    case "--retention-gib": options.RetentionBytes = RetainedBytes(Next(args, ref i)); break;
                    case "--jobs": options.Jobs = Positive(args, ref i); break;
                    case "--gui-jobs": options.GuiJobs = Positive(args, ref i); break;
                    case "--write-jobs": options.WriteJobs = Positive(args, ref i); break;
                    case "--json": options.Json = Next(args, ref i); break;
                    case "--switcher": options.Switcher = Next(args, ref i); break;
                    case "--choices": options.Choices = Next(args, ref i); break;
                    case "--timeout":
                        if (!int.TryParse(Next(args, ref i), out var timeout) || timeout <= 0 || timeout > 86400)
                            throw new ArgumentException("--timeout expects 1..86400 seconds");
                        options.TimeoutSeconds = timeout;
                        break;
                    case "--help": case "-h": options.ShowHelp = true; break;
                    default: throw new ArgumentException($"unknown option '{args[i]}'");
                }
            }
            if (options.Switcher != null && (options.Tests.Count > 0 || options.Mod != null || options.Filter != null || options.Failed != null || options.Profile != null))
                throw new ArgumentException("--switcher cannot be combined with test selection");
            return options;
        }

        private static string Next(string[] args, ref int i)
        {
            if (i + 1 >= args.Length || string.IsNullOrWhiteSpace(args[i + 1]) || args[i + 1].StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException($"option '{args[i]}' requires a value");
            return args[++i];
        }

        private static int Positive(string[] args, ref int i)
        {
            var option = args[i];
            if (!int.TryParse(Next(args, ref i), out var value) || value <= 0)
                throw new ArgumentException(option + " expects a positive integer");
            return value;
        }

        private static double Nonnegative(string text, string option)
        {
            if (!double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value)
                || !double.IsFinite(value) || value < 0) throw new ArgumentException(option + " expects a finite nonnegative number");
            return value;
        }

        private static int RetainedCount(string text)
        {
            if (!int.TryParse(text, out var count) || count < 0) throw new ArgumentException("retention count expects a nonnegative integer");
            return count;
        }

        private static long RetainedBytes(string text)
        {
            var gib = Nonnegative(text, "retention GiB");
            if (gib >= long.MaxValue / (double)(1024L * 1024 * 1024)) throw new ArgumentException("retention GiB is too large");
            return (long)(gib * (1024L * 1024 * 1024));
        }

        public static string HelpText =>
@"Cthangover — тесты ядра, модов и игровых сценариев.
  dotnet run --project tests/Cthangover.TestRunner -- [опции]

Без опций: полный набор в отдельной копии проекта и с отдельными пользовательскими данными.
  --test <glob>        имя/шаблон прогона; можно повторить (--test battle*)
  --mod <id>           тесты мода; с --filter выбирает его [ModTest] кейсы
  --filter <expression> glob кейса мода (*, ?, запятая = ИЛИ); для unit — фильтр dotnet test
  --profile <name>     full, fast, headless, gui, visual, docs
  --kind <kind>        unit, mod-unit, integration, manual
  --tag <tag>          тег каталога
  --game <id>          коллекция игры
  --renderer <id>      topdown или isometric
  --failed <report>    повтор неуспешных прогонов из JSON-отчёта, включая подготовку
  --list               каталог проверок, включая ручные наборы
  --catalog <path>     сохранить каталог в JSON
  --plan               показать порядок, причины выбора и зависимости без запуска
  --artifacts <dir>    корень каталогов запусков (по умолчанию tests/artifacts/runs)
  --json <path>        дополнительная копия общего JSON-отчёта
  --godot <path>       Godot .NET (иначе GODOT_BIN или PATH)
  --no-build           использовать существующую сборку игры
  --fresh-cache        отдельный пустой кеш компиляции / private cold compilation cache
  --jobs <N>           число процессов (по умолчанию половина ядер, 2..8); 1 — последовательный режим
  --gui-jobs <G>       число окон одновременно (по умолчанию 2)
  --write-jobs <W>     число копий для записи (по умолчанию 2)
  --keep-workspace    сохранить копию проекта после успеха (при сбое сохраняется всегда)
  --retention-days <D> возраст неактивных копий / inactive age (default 3; CTHANGOVER_TEST_RETENTION_DAYS)
  --retention-count <N> последние N логических запусков / newest runs (default 3; CTHANGOVER_TEST_RETENTION_COUNT)
  --retention-gib <G> суммарный лимит / total budget (default 20; CTHANGOVER_TEST_RETENTION_GIB)
  --switcher <scene>   smoke-прогон сцены с авто-диалогами
  --choices <0,1,...>  выборы smoke-прогона
  --timeout <seconds>  таймаут авто-диалогов (60 по умолчанию)
  --help               справка

Примеры:
  dotnet run --project tests/Cthangover.TestRunner -- --mod rpg --filter followers/*
  dotnet run --project tests/Cthangover.TestRunner -- --test new_mod_check --plan
  dotnet run --project tests/Cthangover.TestRunner -- --profile fast
  dotnet run --project tests/Cthangover.TestRunner -- --failed tests/artifacts/runs/<id>/report.json";
    }
}
