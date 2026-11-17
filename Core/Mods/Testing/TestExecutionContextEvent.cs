namespace Cthangover.Core.Mods.Testing
{
    using System;
    using System.IO;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Nodes;

    /// <summary>
    /// Publishes process-local bootstrap/exit evidence under the claimed launch root. A verified bootstrap and
    /// CLR exit notification are diagnostics, never replacements for the parent's observed process result.
    /// </summary>
    internal static class TestExecutionContextEvent
    {
        internal static void Write(TestExecutionContext context, string phase, string project, string data,
            string temporary, string nativeEvidence, int? exitCode = null)
        {
            var value = JsonSerializer.SerializeToNode(context, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }).AsObject();
            value["schemaVersion"] = 1;
            value["phase"] = phase;
            value["utc"] = DateTime.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture);
            value["osPid"] = System.Environment.ProcessId;
            value["nativeLogEvidence"] = nativeEvidence;
            value["expected"] = Paths(context.ProjectRoot, context.UserDataRoot, context.TemporaryRoot);
            value["actual"] = Paths(project, data, temporary);
            if (exitCode != null)
            {
                value["clrExitCode"] = exitCode.Value;
            }

            var file = Path.Combine(context.ArtifactRoot, exitCode == null ? "core-context.json" : "core-exit.json");
            var pending = file + "." + System.Environment.ProcessId + ".tmp";
            try
            {
                using (var stream = new FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                {
                    writer.Write(value.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
                    writer.Write('\n');
                }

                File.Move(pending, file);
            }
            finally
            {
                if (File.Exists(pending))
                {
                    File.Delete(pending);
                }
            }
        }

        private static JsonObject Paths(string project, string data, string temporary)
        {
            return new JsonObject
            {
                ["projectRoot"] = project,
                ["userDataRoot"] = data,
                ["temporaryRoot"] = temporary
            };
        }
    }
}
