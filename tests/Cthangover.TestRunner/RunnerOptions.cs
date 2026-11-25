namespace Cthangover.TestRunner
{
    /// <summary>Selection is separate from execution; --plan and --list never build or start a game.</summary>
    public class RunnerOptions
    {
        /// <summary>Repeated catalogue selectors; gated launches additionally require their exact literal name.</summary>
        public List<string> Tests { get; } = new();
        /// <summary>Restricts catalogue owners or one explicitly selected mod-unit host.</summary>
        public string Mod
        {
            get; private set;
        }
        /// <summary>Case expression passed to the selected native mod tests or VSTest without changing their assertions.</summary>
        public string Filter
        {
            get; private set;
        }
        /// <summary>Named catalogue selection; it never bypasses explicit external-handshake admission.</summary>
        public string Profile
        {
            get; private set;
        }
        /// <summary>Catalogue tag restriction applied before prerequisite closure.</summary>
        public string Tag
        {
            get; private set;
        }
        /// <summary>Execution adapter restriction, independently of project read/write access.</summary>
        public string Kind
        {
            get; private set;
        }
        /// <summary>Collection identity restriction for catalogue selection.</summary>
        public string Game
        {
            get; private set;
        }
        /// <summary>Authored renderer variant selector; it does not change screenshot colour settings.</summary>
        public string Renderer
        {
            get; private set;
        }
        /// <summary>Prior run report whose failed selections and prerequisites are repeated with fresh output identities.</summary>
        public string Failed
        {
            get; private set;
        }
        /// <summary>Optional catalogue export path; catalogue-only commands never start children.</summary>
        public string Catalog
        {
            get; private set;
        }
        /// <summary>Parent of unique invocation output directories; independent runs never share a launch root.</summary>
        public string Artifacts
        {
            get; private set;
        }
        /// <summary>Prints selection and preparation policy without preparing inputs or launching a child.</summary>
        public bool Plan
        {
            get; private set;
        }
        /// <summary>Lists catalogue entries, including inert manual entries.</summary>
        public bool List
        {
            get; private set;
        }
        /// <summary>Explicit native executable path; otherwise the established environment/PATH resolver is used.</summary>
        public string Godot
        {
            get; private set;
        }
        /// <summary>Requires matching build receipts and output hashes; DLL presence alone is insufficient.</summary>
        public bool NoBuild
        {
            get; private set;
        }
        /// <summary>Persistent content-fenced input store; warm invocations reuse it without materializing input files.</summary>
        public string InputCache { get; private set; } = TestWorkspace.DefaultInputCache;
        /// <summary>Opts in to reusing live source hashes by path, length and last write time.</summary>
        public bool LazySourceHashes { get; private set; }
        /// <summary>Explicit bounded repair admission for quarantined prepared inputs; never enabled as an automatic fallback.</summary>
        public bool RepairInputs
        {
            get; private set;
        }
        /// <summary>Uses a disposable per-run compilation cache instead of the persistent cache.</summary>
        public bool FreshCache
        {
            get; private set;
        }
        /// <summary>
        /// Optional extra report alias protected against simultaneous writers; the unique run report stays authoritative.
        /// </summary>
        public string Json
        {
            get; private set;
        }
        /// <summary>Logical scene or native scene path for the smoke adapter.</summary>
        public string Switcher
        {
            get; private set;
        }
        /// <summary>Authored autoplay answer sequence for a smoke launch.</summary>
        public string Choices
        {
            get; private set;
        }
        /// <summary>Bounded smoke-driver lifetime, separate from process shutdown supervision.</summary>
        public int TimeoutSeconds { get; private set; } = 60;
        /// <summary>Stops before preparation after displaying command usage.</summary>
        public bool ShowHelp
        {
            get; private set;
        }
        /// <summary>Preserves run-owned state for diagnosis; reusable input retention is independent.</summary>
        public bool KeepWorkspace
        {
            get; private set;
        }
        /// <summary>
        /// Inactive logical runs expire by age, count or total bytes; CLI overrides CTHANGOVER_TEST_RETENTION_* values.
        /// </summary>
        public double RetentionDays { get; private set; } = 3;
        /// <summary>
        /// Maximum inactive logical run/prepared-store owners retained by scoped pruning; active leases are always protected.
        /// </summary>
        public int RetentionCount { get; private set; } = 3;
        /// <summary>
        /// Logical inactive retained-size budget, distinct from copy counters and physical disk writes.
        /// </summary>
        public long RetentionBytes { get; private set; } = 20L * 1024 * 1024 * 1024;

        /// <summary>Global child limit; one schedules sequentially over the same verified preparation protocol.</summary>
        public int Jobs { get; private set; } = Math.Clamp(Environment.ProcessorCount / 2, 2, 8);
        /// <summary>Windowed children also count against the global limit.</summary>
        public int GuiJobs { get; private set; } = 2;
        /// <summary>Maximum persistent write lanes; dependency components retain exclusive ownership until complete.</summary>
        public int WriteJobs { get; private set; } = 2;

        /// <summary>Validates command selectors, finite limits and preparation options before any native process is admitted.</summary>
        public static RunnerOptions Parse(string[] args)
        {
            var options = new RunnerOptions();
            options.RetentionDays = Nonnegative(
                Environment.GetEnvironmentVariable("CTHANGOVER_TEST_RETENTION_DAYS") ?? "3", "CTHANGOVER_TEST_RETENTION_DAYS");
            options.RetentionCount = RetainedCount(Environment.GetEnvironmentVariable("CTHANGOVER_TEST_RETENTION_COUNT") ?? "3");
            options.RetentionBytes = RetainedBytes(Environment.GetEnvironmentVariable("CTHANGOVER_TEST_RETENTION_GIB") ?? "20");
            for (var i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--test":
                        options.Tests.Add(Next(args, ref i));
                        break;
                    case "--mod":
                        options.Mod = Next(args, ref i);
                        break;
                    case "--filter":
                        options.Filter = Next(args, ref i);
                        break;
                    case "--profile":
                        options.Profile = Next(args, ref i);
                        break;
                    case "--tag":
                        options.Tag = Next(args, ref i);
                        break;
                    case "--kind":
                        options.Kind = Next(args, ref i);
                        break;
                    case "--game":
                        options.Game = Next(args, ref i);
                        break;
                    case "--renderer":
                        options.Renderer = Next(args, ref i);
                        break;
                    case "--failed":
                        options.Failed = Next(args, ref i);
                        break;
                    case "--catalog":
                        options.Catalog = Next(args, ref i);
                        break;
                    case "--artifacts":
                        options.Artifacts = Next(args, ref i);
                        break;
                    case "--plan":
                        options.Plan = true;
                        break;
                    case "--list":
                        options.List = true;
                        break;
                    case "--godot":
                        options.Godot = Next(args, ref i);
                        break;
                    case "--no-build":
                        options.NoBuild = true;
                        break;
                    case "--input-cache":
                        options.InputCache = Path.GetFullPath(Next(args, ref i));
                        break;
                    case "--lazy-source-hashes":
                        options.LazySourceHashes = true;
                        break;
                    case "--repair-inputs":
                        options.RepairInputs = true;
                        break;
                    case "--fresh-cache":
                        options.FreshCache = true;
                        break;
                    case "--keep-workspace":
                        options.KeepWorkspace = true;
                        break;
                    case "--retention-days":
                        options.RetentionDays = Nonnegative(Next(args, ref i), "--retention-days");
                        break;
                    case "--retention-count":
                        options.RetentionCount = RetainedCount(Next(args, ref i));
                        break;
                    case "--retention-gib":
                        options.RetentionBytes = RetainedBytes(Next(args, ref i));
                        break;
                    case "--jobs":
                        options.Jobs = Positive(args, ref i);
                        break;
                    case "--gui-jobs":
                        options.GuiJobs = Positive(args, ref i);
                        break;
                    case "--write-jobs":
                        options.WriteJobs = Positive(args, ref i);
                        break;
                    case "--json":
                        options.Json = Next(args, ref i);
                        break;
                    case "--switcher":
                        options.Switcher = Next(args, ref i);
                        break;
                    case "--choices":
                        options.Choices = Next(args, ref i);
                        break;
                    case "--timeout":
                        if (!int.TryParse(Next(args, ref i), out var timeout) || timeout <= 0 || timeout > 86400)
                            throw new ArgumentException("--timeout expects 1..86400 seconds");
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
            if (options.Switcher != null && (options.Tests.Count > 0 || options.Mod != null || options.Filter != null
                || options.Failed != null || options.Profile != null))
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
            if (!double.TryParse(text, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var value)
                || !double.IsFinite(value) || value < 0)
                throw new ArgumentException(option + " expects a finite nonnegative number");
            return value;
        }

        private static int RetainedCount(string text)
        {
            if (!int.TryParse(text, out var count) || count < 0)
                throw new ArgumentException("retention count expects a nonnegative integer");
            return count;
        }

        private static long RetainedBytes(string text)
        {
            var gib = Nonnegative(text, "retention GiB");
            if (gib >= long.MaxValue / (double)(1024L * 1024 * 1024))
                throw new ArgumentException("retention GiB is too large");
            return (long)(gib * (1024L * 1024 * 1024));
        }

        /// <summary>Documents selectors and measured preparation policies for invoking the already built runner.</summary>
        public static string HelpText =>
@"Cthangover — тесты ядра, модов и игровых сценариев.
  dotnet run --project tests/Cthangover.TestRunner -- [опции]

Без опций: полный набор с проверенными общими входами и отдельными пользовательскими данными.
Полные прогоны — только через задачу вотчера на хосте.
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
  --no-build           проверить receipt и хеши готовой сборки; при несовпадении — отказ
  --input-cache <dir>  кеш неизменяемых входов; повтор той же версии не копирует входы
  --lazy-source-hashes  переиспользовать SHA-256 исходников по пути, размеру и дате записи
  --repair-inputs     явно восстановить quarantined-входы; обычный запуск отказывает
  --fresh-cache        отдельный пустой кеш компиляции / private cold compilation cache
  --jobs <N>           число процессов (по умолчанию половина ядер, 2..8); 1 — последовательный режим
  --gui-jobs <G>       число окон одновременно (по умолчанию 2)
  --write-jobs <W>     число постоянных изолированных write lanes (по умолчанию 2)
  --keep-workspace    сохранить данные/диагностику после успеха; кеш входов хранится отдельно
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
