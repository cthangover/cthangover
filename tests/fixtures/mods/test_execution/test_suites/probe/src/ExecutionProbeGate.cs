namespace Cthangover.Mods.ExecutionProbe
{
    using System;
    using System.IO;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>Waits for a launch-matched atomic command with a bounded file notification gate, without polling sleeps.</summary>
    public sealed class ExecutionProbeGate : IDisposable
    {
        private readonly string commandPath;
        private readonly string launchId;
        private readonly FileSystemWatcher watcher;
        private readonly TaskCompletionSource<string> command = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Arms the watcher before any readiness marker is published; an already present command is also checked.</summary>
        public ExecutionProbeGate(string artifactRoot, string launchId)
        {
            this.launchId = launchId;
            commandPath = Path.Combine(artifactRoot, "probe-release.json");
            watcher = new FileSystemWatcher(artifactRoot, "probe-release.json");
            watcher.Created += Changed;
            watcher.Changed += Changed;
            watcher.Renamed += Renamed;
            watcher.Error += Failed;
            watcher.EnableRaisingEvents = true;
            ReadCommand();
        }

        /// <summary>Accepts release or cooperative cancel only for this launch; deadline or malformed input fails the test.</summary>
        public async Task Wait(CancellationToken cancellationToken)
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(TimeSpan.FromSeconds(60));
            var value = await command.Task.WaitAsync(deadline.Token);
            if (value == "cancel")
            {
                throw new OperationCanceledException("Execution probe received an explicit cancel command.", deadline.Token);
            }
        }

        private void Changed(object sender, FileSystemEventArgs args)
        {
            ReadCommand();
        }

        private void Renamed(object sender, RenamedEventArgs args)
        {
            ReadCommand();
        }

        private void Failed(object sender, ErrorEventArgs args)
        {
            command.TrySetException(args.GetException());
        }

        private void ReadCommand()
        {
            try
            {
                if (!File.Exists(commandPath))
                {
                    return;
                }

                using var document = JsonDocument.Parse(File.ReadAllText(commandPath));
                var root = document.RootElement;
                var value = root.GetProperty("command").GetString();
                if (root.GetProperty("schemaVersion").GetInt32() != 1 || root.GetProperty("launchId").GetString() != launchId
                    || value is not ("release" or "cancel"))
                {
                    throw new InvalidDataException("Execution probe command does not match its version, launch or command contract.");
                }

                command.TrySetResult(value);
            }
            catch (Exception error) when (error is IOException or JsonException or InvalidOperationException or KeyNotFoundException)
            {
                command.TrySetException(error);
            }
        }

        /// <summary>Detaches native watcher handles on every success, timeout, cancellation and assertion failure.</summary>
        public void Dispose()
        {
            watcher.EnableRaisingEvents = false;
            watcher.Created -= Changed;
            watcher.Changed -= Changed;
            watcher.Renamed -= Renamed;
            watcher.Error -= Failed;
            watcher.Dispose();
        }
    }
}
