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
        public string Json { get; private set; }
        public string Switcher { get; private set; }
        public string Choices { get; private set; }
        public int TimeoutSeconds { get; private set; } = 60;
        public bool ShowHelp { get; private set; }
        public bool KeepWorkspace { get; private set; }

        public static RunnerOptions Parse(string[] args)
        {
            var options = new RunnerOptions();
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
                    case "--keep-workspace": options.KeepWorkspace = true; break;
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
  --keep-workspace    сохранить копию проекта после успеха (при сбое сохраняется всегда)
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
