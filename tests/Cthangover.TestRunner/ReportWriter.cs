namespace Cthangover.TestRunner
{
    using System.Globalization;
    using System.Net;
    using System.Text;
    using System.Text.Json;
    using System.Xml.Linq;
    using System.Diagnostics;

    /// <summary>Writes the same outcomes as durable JSON, portable JUnit and a browsable HTML report.</summary>
    public static class ReportWriter
    {
        public static readonly JsonSerializerOptions JsonOptions = new()
        { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true };

        public static void Write(string directory, RunReport report, string extraJson = null)
        {
            var started = Stopwatch.GetTimestamp();
            Directory.CreateDirectory(directory);
            if (report.Complete)
            {
                AtomicWrite(Path.Combine(directory, "report.junit.xml"), JUnit(report).ToString());
                AtomicWrite(Path.Combine(directory, "report.html"), Html(report, directory));
            }
            report.Phases.ReportWritingSeconds += Stopwatch.GetElapsedTime(started).TotalSeconds;
            started = Stopwatch.GetTimestamp();
            try
            {
                var json = JsonSerializer.Serialize(report, JsonOptions);
                AtomicWrite(Path.Combine(directory, "report.json"), json);
                if (extraJson != null) AtomicWrite(extraJson, json);
            }
            finally { report.Phases.ReportWritingSeconds += Stopwatch.GetElapsedTime(started).TotalSeconds; }
        }

        private static void AtomicWrite(string path, string text)
        {
            path = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllText(temporary, text, new UTF8Encoding(false));
            File.Move(temporary, path, true);
        }

        public static XElement JUnit(RunReport report)
        {
            var root = new XElement("testsuites", new XAttribute("name", "cthangover"));
            foreach (var result in Entries(report))
            {
                var suite = new XElement("testsuite", new XAttribute("name", result.Name), new XAttribute("time", Number(result.DurationSeconds)));
                if (result.Cases.Count == 0)
                    suite.Add(Case(result.Name, result.Name, !result.Passed && !result.Blocked && result.FailureKind is not (null or "assertion") ? "error" : result.Status,
                        result.DurationSeconds, result.Note, string.Join("\n", result.Diagnostics)));
                else
                {
                    foreach (var item in result.Cases)
                        suite.Add(Case(result.Name, (item.Mod == null ? "" : item.Mod + "/") + item.Name, item.Status, item.DurationSeconds, item.Message, item.StackTrace));
                    if (!result.Passed && result.Cases.All(c => c.Status is "pass" or "skipped"))
                        suite.Add(Case(result.Name, "<infrastructure>", "error", 0, result.Note, string.Join("\n", result.Diagnostics)));
                }
                suite.SetAttributeValue("tests", suite.Elements("testcase").Count());
                suite.SetAttributeValue("failures", suite.Descendants("failure").Count());
                suite.SetAttributeValue("errors", suite.Descendants("error").Count());
                suite.SetAttributeValue("skipped", suite.Descendants("skipped").Count());
                root.Add(suite);
            }
            if (!report.Complete)
                root.Add(new XElement("testsuite", new XAttribute("name", "run-completion"), new XAttribute("tests", 1), new XAttribute("errors", 1),
                    Case("run", "completion", "error", 0, "run is incomplete", "The runner has not recorded final completion.")));
            return root;
        }

        private static XElement Case(string suite, string name, string status, double seconds, string message, string stack)
        {
            var element = new XElement("testcase", new XAttribute("classname", suite), new XAttribute("name", name), new XAttribute("time", Number(seconds)));
            if (status is "skipped" or "blocked") element.Add(new XElement("skipped", new XAttribute("message", message ?? status)));
            else if (status != "pass") element.Add(new XElement(status is "error" or "timeout" ? "error" : "failure", new XAttribute("message", message ?? status), stack ?? ""));
            return element;
        }

        private static string Number(double value) => value.ToString("0.######", CultureInfo.InvariantCulture);

        public static string Html(RunReport report, string directory = null)
        {
            var html = new StringBuilder("<!doctype html><html lang=\"en\"><meta charset=\"utf-8\"><title>Cthangover tests</title><style>body{font:16px system-ui;max-width:1200px;margin:2em auto;padding:0 1em;background:#171b24;color:#eee}a{color:#8bd}td,th{padding:.5em;text-align:left;border-bottom:1px solid #456}pre{white-space:pre-wrap}summary{cursor:pointer}</style>");
            html.Append("<h1>Cthangover tests — ").Append(WebUtility.HtmlEncode(report.RunId)).Append("</h1><p>")
                .Append(report.Complete ? report.Success ? "PASS" : "FAIL" : "RUNNING / INCOMPLETE").Append("</p><table><tr><th>Test</th><th>Result</th><th>Passed / Failed / Skipped</th><th>Seconds</th></tr>");
            foreach (var result in Entries(report))
                html.Append("<tr><td><a href=\"#").Append(TestWorkspace.Key(result.Name)).Append("\">").Append(WebUtility.HtmlEncode(result.Name)).Append("</a></td><td>")
                    .Append(WebUtility.HtmlEncode(result.Status + (result.FailureKind == null ? "" : " (" + result.FailureKind + ")"))).Append("</td><td>").Append(result.PassedChecks).Append(" / ").Append(result.FailedChecks).Append(" / ").Append(result.SkippedChecks)
                    .Append("</td><td>").Append(Number(result.DurationSeconds)).Append("</td></tr>");
            html.Append("</table>");
            Timing(html, report);
            foreach (var result in Entries(report))
            {
                html.Append("<h2 id=\"").Append(TestWorkspace.Key(result.Name)).Append("\">").Append(WebUtility.HtmlEncode(result.Name)).Append("</h2><pre>")
                    .Append(WebUtility.HtmlEncode(result.Note)).Append("</pre>");
                if (result.LogFile != null) html.Append("<p><a href=\"").Append(WebUtility.HtmlEncode(Path.GetRelativePath(directory ?? Directory.GetCurrentDirectory(), result.LogFile).Replace('\\', '/'))).Append("\">Log path: ")
                    .Append(WebUtility.HtmlEncode(result.LogFile)).Append("</a></p>");
                foreach (var item in result.Cases.Where(c => c.Status != "pass"))
                    html.Append("<details><summary>").Append(WebUtility.HtmlEncode(item.Status + " " + item.Mod + "/" + item.Name)).Append("</summary><pre>")
                        .Append(WebUtility.HtmlEncode(item.Message + "\n" + item.StackTrace)).Append("</pre></details>");
                if (result.Artifacts.Count > 0)
                {
                    html.Append("<details><summary>Artifacts (").Append(result.Artifacts.Count).Append(")</summary><ul>");
                    foreach (var artifact in result.Artifacts)
                        html.Append("<li><a href=\"").Append(WebUtility.HtmlEncode(Path.GetRelativePath(directory ?? Directory.GetCurrentDirectory(), artifact).Replace('\\', '/')))
                            .Append("\">").Append(WebUtility.HtmlEncode(Path.GetFileName(artifact))).Append("</a></li>");
                    html.Append("</ul></details>");
                }
                html.Append("<pre>").Append(WebUtility.HtmlEncode(string.Join("\n", result.Diagnostics))).Append("</pre>");
            }
            return html.Append("</html>\n").ToString();
        }

        private static IEnumerable<TestResult> Entries(RunReport report) => report.Build == null ? report.Results : report.Results.Prepend(report.Build);

        private static void Timing(StringBuilder html, RunReport report)
        {
            var phases = report.Phases;
            html.Append("<h2>Time breakdown / Затраты времени</h2><p>Wall / Всего: ").Append(Number(report.WallSeconds))
                .Append(" s; children / процессы: ").Append(Number(report.ChildSeconds)).Append(" s</p><table>");
            TimingRow(html, "Build / Сборка", phases.BuildSeconds);
            TimingRow(html, "Workspace copy / Копирование", phases.WorkspaceCopySeconds);
            TimingRow(html, "Restore / Восстановление (" + phases.RestoreCount + ")", phases.RestoreSeconds);
            TimingRow(html, "Artifacts / Артефакты", phases.ArtifactCollectionSeconds);
            TimingRow(html, "Reports / Отчёты", phases.ReportWritingSeconds);
            TimingRow(html, "Cleanup / Очистка", phases.CleanupSeconds);
            TimingRow(html, "Launch wall / Запуски", phases.LaunchSeconds);
            html.Append("<tr><td>Copies / Копии</td><td>").Append(phases.Copies)
                .Append("</td></tr><tr><td>Parallelism / Параллелизм</td><td>").Append(Number(phases.Parallelism)).Append("</td></tr>");
            foreach (var lane in phases.LaneBusySeconds) TimingRow(html, "Lane / Копия " + lane.Key, lane.Value);
            html.Append("</table><h3>Slowest launches / Самые долгие прогоны</h3><table><tr><th>Launch / Прогон</th><th>Process s</th><th>Overhead s</th><th>Boot s</th><th>CPU s</th><th>MiB</th><th>Log lines</th></tr>");
            foreach (var result in report.Results.OrderByDescending(Duration).Take(10))
                html.Append("<tr><td>").Append(WebUtility.HtmlEncode(result.Name)).Append("</td><td>").Append(Number(result.DurationSeconds))
                    .Append("</td><td>").Append(Number(result.OverheadSeconds)).Append("</td><td>").Append(Optional(result.BootSeconds))
                    .Append("</td><td>").Append(Optional(result.CpuSeconds)).Append("</td><td>").Append(Optional(result.PeakMemoryMb))
                    .Append("</td><td>").Append(result.LogLines).Append("</td></tr>");
            html.Append("</table>");
        }

        private static double Duration(TestResult result) => result.DurationSeconds;
        private static string Optional(double? value) => value.HasValue ? Number(value.Value) : "—";
        private static void TimingRow(StringBuilder html, string name, double seconds)
            => html.Append("<tr><td>").Append(WebUtility.HtmlEncode(name)).Append("</td><td>").Append(Number(seconds)).Append(" s</td></tr>");
    }
}
