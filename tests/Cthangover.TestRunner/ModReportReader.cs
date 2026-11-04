namespace Cthangover.TestRunner
{
    using System.Text.Json;

    /// <summary>Imports leaf outcomes and checks that the JSON report agrees with the process terminal marker.</summary>
    public static class ModReportReader
    {
        public static void Import(string path, TestResult result)
        {
            if (!File.Exists(path)) throw new InvalidOperationException("missing mod-test JSON report");
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            foreach (var suite in document.RootElement.GetProperty("Suites").EnumerateArray())
            {
                var mod = suite.GetProperty("ModId").GetString();
                foreach (var test in suite.GetProperty("Tests").EnumerateArray())
                {
                    var status = test.GetProperty("Status").GetString() switch
                    { "Passed" => "pass", "Skipped" => "skipped", "Timeout" => "timeout", "Error" => "error", "Failed" => "fail", var other => throw new InvalidOperationException("unknown mod-test status: " + other) };
                    result.Cases.Add(new TestCaseResult
                    {
                        Mod = mod, Name = test.GetProperty("TestName").GetString(), Status = status,
                        DurationSeconds = test.GetProperty("DurationMs").GetDouble() / 1000,
                        Message = Text(test, "Message"), StackTrace = Text(test, "StackTrace"),
                        Log = Strings(test, "Log"), Artifacts = Strings(test, "Artifacts"),
                    });
                }
            }
            if (result.Cases.Count == 0) throw new InvalidOperationException("empty mod-test report");
            if (result.Cases.Any(test => test.Name == "<compile>" && test.Status == "error")) result.FailureKind = "compilation";
            else if (result.Cases.Any(test => test.Name == "<discovery>" && test.Status == "error")) result.FailureKind = "discovery";
            else if (result.Cases.Any(test => test.Status == "timeout")) result.FailureKind = "timeout";
            else if (result.Cases.Any(test => test.Name.EndsWith(" [recovery]", StringComparison.Ordinal))) result.FailureKind = "recovery";
            if (result.Cases.Select(c => c.Mod + "/" + c.Name).Distinct(StringComparer.Ordinal).Count() != result.Cases.Count)
                throw new InvalidOperationException("duplicate case in mod-test report");
            if (result.PassedChecks != result.Cases.Count(c => c.Status == "pass")
                || result.SkippedChecks != result.Cases.Count(c => c.Status == "skipped")
                || result.FailedChecks != result.Cases.Count(c => c.Status is "fail" or "timeout" or "error"))
                throw new InvalidOperationException("mod-test report does not match terminal counters");
        }

        private static string Text(JsonElement element, string name) => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        private static List<string> Strings(JsonElement element, string name) => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().Select(item => item.GetString()).ToList() : new();
    }
}
