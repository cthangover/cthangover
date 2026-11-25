namespace Cthangover.Mods.ExecutionEditorWrite
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>Arms a launch-matched start command before writer readiness; startup never consumes the read peer's live gate.</summary>
    public sealed class ExecutionEditorWriteGate : IDisposable
    {
        private readonly string path;
        private readonly string launchId;
        private readonly FileSystemWatcher watcher;
        private readonly TaskCompletionSource<bool> started = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Listens for an atomic editor-start.json with schema1, this LaunchId and command=start.</summary>
        public ExecutionEditorWriteGate(string artifactRoot, string launchId)
        {
            this.launchId = launchId;
            path = Path.Combine(artifactRoot, "editor-start.json");
            watcher = new FileSystemWatcher(artifactRoot, "editor-start.json");
            watcher.Created += Changed;
            watcher.Changed += Changed;
            watcher.Renamed += Renamed;
            watcher.Error += Failed;
            watcher.EnableRaisingEvents = true;
            Read();
        }

        /// <summary>Only the explicit E command starts mutations; timeout fails instead of guessing that the peer is ready.</summary>
        public Task Wait(CancellationToken cancellationToken)
        {
            return started.Task.WaitAsync(TimeSpan.FromSeconds(180), cancellationToken);
        }

        private void Changed(object sender, FileSystemEventArgs args)
        {
            Read();
        }

        private void Renamed(object sender, RenamedEventArgs args)
        {
            Read();
        }

        private void Failed(object sender, ErrorEventArgs args)
        {
            started.TrySetException(args.GetException());
        }

        private void Read()
        {
            try
            {
                if (!File.Exists(path))
                {
                    return;
                }

                using var document = JsonDocument.Parse(File.ReadAllText(path));
                var root = document.RootElement;
                if (root.GetProperty("schemaVersion").GetInt32() != 1
                    || root.GetProperty("launchId").GetString() != launchId
                    || root.GetProperty("command").GetString() != "start")
                {
                    throw new InvalidDataException("Editor start command does not match schema, launch or command=start.");
                }

                started.TrySetResult(true);
            }
            catch (Exception error) when (error is IOException or JsonException or InvalidOperationException or KeyNotFoundException)
            {
                started.TrySetException(error);
            }
        }

        /// <summary>Releases all watcher handlers after completion, refusal or cancellation.</summary>
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
