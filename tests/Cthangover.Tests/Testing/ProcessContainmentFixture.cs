namespace Cthangover.Tests.Testing
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Cthangover.TestRunner;
    using Engine.Tests.Arch;

    /// <summary>Builds a tiny dedicated supervisor so intentionally unsafe inherited pipes cannot poison the xUnit process.</summary>
    internal sealed class ProcessContainmentFixture : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "cthangover-containment-" + Guid.NewGuid().ToString("N"));
        private bool cleanupSafe = true;

        internal JsonDocument Run()
        {
            var build = Path.Combine(root, "helper");
            Directory.CreateDirectory(build);
            var source = Path.Combine(RepoPaths.FindRepoRoot(), "tests", "fixtures", "process_containment");
            File.Copy(Path.Combine(source, "ProcessContainment.csproj"), Path.Combine(build, "ProcessContainment.csproj"));
            File.Copy(Path.Combine(source, "Program.cs"), Path.Combine(build, "Program.cs"));
            var start = new ProcessStartInfo("dotnet");
            start.ArgumentList.Add("build");
            start.ArgumentList.Add(Path.Combine(build, "ProcessContainment.csproj"));
            start.ArgumentList.Add("--nologo");
            start.ArgumentList.Add("-p:RunnerAssembly=" + typeof(ProcessRunner).Assembly.Location);
            Execute(start, "build", 30);
            var supervisor = new ProcessStartInfo("dotnet");
            supervisor.ArgumentList.Add(Path.Combine(build, "bin", "Debug", "net10.0", "ProcessContainment.dll"));
            supervisor.ArgumentList.Add("supervisor");
            supervisor.ArgumentList.Add(root);
            var temporary = Path.Combine(root, "temp");
            Directory.CreateDirectory(temporary);
            supervisor.Environment["TMP"] = temporary;
            supervisor.Environment["TEMP"] = temporary;
            supervisor.Environment["TMPDIR"] = temporary;
            cleanupSafe = false;
            Execute(supervisor, "supervisor", 20);
            var result = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "result.json")));
            cleanupSafe = result.RootElement.GetProperty("childExitConfirmed").GetBoolean();
            var evidence = Environment.GetEnvironmentVariable("CTHANGOVER_PROCESS_CONTAINMENT_EVIDENCE");
            if (!string.IsNullOrWhiteSpace(evidence))
            {
                Directory.CreateDirectory(evidence);
                File.Copy(Path.Combine(root, "result.json"), Path.Combine(evidence, "result.json"), true);
                File.Copy(Path.Combine(root, "supervisor.stdout.log"), Path.Combine(evidence, "supervisor.stdout.log"), true);
                File.Copy(Path.Combine(root, "supervisor.stderr.log"), Path.Combine(evidence, "supervisor.stderr.log"), true);
            }

            return result;
        }

        private void Execute(ProcessStartInfo start, string name, int seconds)
        {
            start.UseShellExecute = false;
            start.RedirectStandardOutput = true;
            start.RedirectStandardError = true;
            start.WorkingDirectory = root;
            using var process = Process.Start(start);
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(seconds * 1000))
            {
                process.Kill(true);
                process.WaitForExit(5000);
                throw new TimeoutException(name + " exceeded " + seconds + " seconds; diagnostics retained: " + root);
            }

            if (!Task.WhenAll(stdout, stderr).Wait(TimeSpan.FromSeconds(3)))
            {
                throw new TimeoutException(name + " outer evidence pipes did not close; diagnostics retained: " + root);
            }

            File.WriteAllText(Path.Combine(root, name + ".stdout.log"), stdout.GetAwaiter().GetResult());
            File.WriteAllText(Path.Combine(root, name + ".stderr.log"), stderr.GetAwaiter().GetResult());
            Assert.Equal(0, process.ExitCode);
        }

        /// <summary>Unique scratch is removed only after supervisor exit and independently observed controlled descendant exit.</summary>
        public void Dispose()
        {
            if (cleanupSafe && Directory.Exists(root))
            {
                foreach (var file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
                {
                    File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
                }

                Directory.Delete(root, true);
            }
        }
    }
}
