namespace Cthangover.Mods.ExecutionEditorWrite
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Text.Json.Nodes;
    using System.Threading;
    using System.Threading.Tasks;
    using Cthangover.Core.Bootstrap;
    using Cthangover.Core.Mods.Resolvers;
    using Cthangover.Core.Mods.Testing;
    using Cthangover.Core.Utils;
    using Cthangover.Mods.Interface.Base;
    using Cthangover.Mods.Interface.Tool;
    using Cthangover.Mods.Rpg.Model;
    using Cthangover.Tools.RpgObjectEditor;
    using Cthangover.Tools.Services;
    using Godot;

    /// <summary>Waits after real editor startup, then exercises two actual saves/reopens and restores all owned authoring bytes.</summary>
    public sealed partial class ExecutionEditorWriteDriver : ModNode
    {
        private const int ExpectedChecks = 12;
        private readonly CancellationTokenSource lifetime = new();
        private RpgObjectEditorWindow window;
        private SceneTree tree;
        private TestExecutionContext context;
        private ExecutionEditorWriteFiles files;
        private Dictionary<string, object> before;
        private string createdId;
        private int passed;
        private string error;
        private Stopwatch work;

        /// <summary>Startup and document construction finish before any externally held read-peer deadline begins.</summary>
        protected override void OnReady()
        {
            if (GameEnvironment.IsRuntime)
            {
                tree = GetTree();
                _ = Run();
            }
        }

        private async Task Run()
        {
            try
            {
                context = TestExecutionContextRuntime.Current
                    ?? throw new InvalidOperationException("Editor lifecycle requires a validated launch context.");
                ModAssert.Equal("write", context.Access, "physical editor lane");
                var startup = Stopwatch.StartNew();
                while (!Boot.IsReady)
                {
                    RequireTime(startup, 120, "editor boot");
                    await Frame();
                }

                window = ToolWindow.Open<RpgObjectEditorWindow>();
                while (window.Document == null)
                {
                    RequireTime(startup, 120, "editor document startup");
                    await Frame();
                }

                window.OpenMod(ExecutionEditorWriteFiles.Mod);
                await Frame();
                ModAssert.NotNull(window.Document, "real editor document");
                files = new ExecutionEditorWriteFiles();
                before = files.Snapshot();
                using var gate = new ExecutionEditorWriteGate(context.ArtifactRoot, context.LaunchId);
                Publish("editor-before.json", "writer_ready", "before");
                Publish("editor-ready.json", "writer_ready", "ready");
                Stage("writer_ready", "ready");
                GameLogger.Log("TEST_EXECUTION", "[EXECUTION_EDITOR_READY] launch=" + context.LaunchId);
                await gate.Wait(lifetime.Token);
                work = Stopwatch.StartNew();
                await Exercise();
            }
            catch (Exception failure)
            {
                error = failure.ToString();
                GameLogger.Log("TEST_EXECUTION", "[EXECUTION_EDITOR_FAILURE] " + error, LogLevel.Error);
            }
            finally
            {
                var successful = false;
                try
                {
                    try
                    {
                        await CloseWindow();
                    }
                    catch (Exception failure)
                    {
                        RecordFailure("Window close failed", failure);
                    }

                    try
                    {
                        if (files != null)
                        {
                            files.Restore();
                            Check(files.MatchesBefore(), "exact scoped resources restored");
                            var removed = createdId == null || !Entities.Collect<RpgObject>().ContainsKey(createdId);
                            Check(removed, "created runtime object removed");
                            RequireWorkTime();
                        }
                    }
                    catch (Exception failure)
                    {
                        RecordFailure("Resource restoration failed", failure);
                    }

                    if (context != null)
                    {
                        var restored = files != null && files.MatchesBefore();
                        var complete = error == null && restored && passed == ExpectedChecks;
                        Publish("editor-after.json", complete ? "restored" : "failed", "after");
                        Stage(complete ? "restored" : "failed", "complete");
                        Publish("editor-result.json", complete ? "restored" : "failed", "complete", true);
                        successful = complete;
                    }
                }
                catch (Exception failure)
                {
                    RecordFailure("Final receipt publication failed", failure);
                }
                finally
                {
                    try
                    {
                        if (error != null)
                        {
                            GameLogger.Log("TEST_EXECUTION", error, LogLevel.Error);
                        }

                        GameLogger.Log("TEST", "[TEST_RESULT] name=test_execution_editor_write passed=" + passed
                            + " failed=" + (successful ? 0 : 1));
                    }
                    finally
                    {
                        ApplicationExit.Quit(tree, successful ? 0 : 1);
                    }
                }
            }
        }

        private void RecordFailure(string description, Exception failure)
        {
            error = (error ?? string.Empty) + "\n" + description + ": " + failure;
        }

        private async Task Exercise()
        {
            var expectedId = window.Document.FreeId(ExecutionEditorWriteFiles.Mod + "/new_object");
            Stage("working", "create");
            Press("tools/entity_editor/new");
            createdId = window.SelectedId;
            var record = window.Document.Find(createdId);
            Check(createdId == expectedId && record != null && !record.IsForeign, "New creates an own record");
            Check(record.File == ExecutionEditorWriteFiles.Objects, "New uses actual editor output file");
            TypeAnchor("0.25, 0.75");
            Check(Anchor(record.Data, .25, .75), "first anchor form edit");
            Stage("working", "save_first");
            Press("tools/common/save");
            Check(Persisted(.25, .75), "first save persisted actual bytes");
            Check(RuntimeAnchor(.25f, .75f), "first save refreshed runtime resolver");
            await Reopen();
            Check(Anchor(window.Document.Find(createdId)?.Data, .25, .75), "first close/reopen readback");
            Stage("working", "change");
            TypeAnchor("0.5, 0.25");
            Check(Anchor(window.Document.Find(createdId)?.Data, .5, .25), "second anchor form edit");
            Stage("working", "save_second");
            Press("tools/common/save");
            Check(Persisted(.5, .25), "second save persisted actual bytes");
            Check(RuntimeAnchor(.5f, .25f), "second save refreshed runtime resolver");
            await Reopen();
            Check(Anchor(window.Document.Find(createdId)?.Data, .5, .25), "second close/reopen readback");
            Stage("working", "restore");
        }

        private async Task Reopen()
        {
            await CloseWindow();
            RequireWorkTime();
            window = ToolWindow.Open<RpgObjectEditorWindow>();
            window.OpenMod(ExecutionEditorWriteFiles.Mod);
            window.Select(window.Document.Find(createdId));
            await Frame();
            RequireWorkTime();
        }

        private async Task CloseWindow()
        {
            if (window == null || !GodotObject.IsInstanceValid(window))
            {
                window = null;
                return;
            }

            var closing = window;
            window = null;
            closing.QueueFree();
            var deadline = Stopwatch.StartNew();
            while (GodotObject.IsInstanceValid(closing))
            {
                RequireTime(deadline, 5, "editor window destruction");
                await Frame();
            }
        }

        private async Task Frame()
        {
            lifetime.Token.ThrowIfCancellationRequested();
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }

        private void Press(string key)
        {
            RequireWorkTime();
            var text = TranslationServer.Translate(key).ToString();
            foreach (var node in window.FindChildren("*", "", true, false))
            {
                if (node is Button button && button.Text == text)
                {
                    button.EmitSignal(BaseButton.SignalName.Pressed);
                    return;
                }
            }

            throw new InvalidOperationException("Actual editor button missing: " + key);
        }

        private void TypeAnchor(string text)
        {
            RequireWorkTime();
            var translated = TranslationServer.Translate("tools/rpg_object_editor/anchor").ToString();
            foreach (var node in window.FindChildren("*", "", true, false))
            {
                if (node is not Label label || label.Text != translated || !label.IsVisibleInTree())
                {
                    continue;
                }

                foreach (var child in label.GetParent().GetChildren())
                {
                    if (child is LineEdit edit)
                    {
                        edit.Text = text;
                        edit.EmitSignal(LineEdit.SignalName.TextChanged, text);
                        return;
                    }
                }
            }

            throw new InvalidOperationException("Actual anchor form input is unavailable.");
        }

        private bool Persisted(double x, double y)
        {
            var root = JsonNode.Parse(ModFileService.ReadText(ExecutionEditorWriteFiles.Mod, ExecutionEditorWriteFiles.Objects));
            if (root?["Items"] is JsonArray records)
            {
                foreach (var record in records)
                {
                    if (record?["Id"]?.ToString() == createdId)
                    {
                        return Anchor(record as JsonObject, x, y);
                    }
                }
            }

            return false;
        }

        private bool RuntimeAnchor(float x, float y)
        {
            return Entities.Collect<RpgObject>().TryGetValue(createdId, out var item)
                && item.Anchor is { Length: 2 } && item.Anchor[0] == x && item.Anchor[1] == y;
        }

        private static bool Anchor(JsonObject record, double x, double y)
        {
            return record?["Anchor"] is JsonArray { Count: 2 } anchor
                && double.Parse(anchor[0].ToJsonString(), CultureInfo.InvariantCulture) == x
                && double.Parse(anchor[1].ToJsonString(), CultureInfo.InvariantCulture) == y;
        }

        private void Check(bool condition, string description)
        {
            ModAssert.True(condition, description);
            passed++;
        }

        private static void RequireTime(Stopwatch watch, double seconds, string description)
        {
            if (watch.Elapsed.TotalSeconds > seconds)
            {
                throw new TimeoutException(description + " exceeded its declared " + seconds + " second budget.");
            }
        }

        private void RequireWorkTime()
        {
            if (work != null)
            {
                RequireTime(work, 45, "editor create/save/reopen/restore");
            }
        }

        private void Stage(string phase, string stage)
        {
            Publish("editor-stage.json", phase, stage);
            GameLogger.Log("TEST_EXECUTION", "[EXECUTION_EDITOR_STAGE] launch=" + context.LaunchId + " stage=" + stage);
        }

        private void Publish(string name, string phase, string stage, bool complete = false)
        {
            var record = new Dictionary<string, object>
            {
                ["schemaVersion"] = 1,
                ["phase"] = phase,
                ["stage"] = stage,
                ["runId"] = context.RunId,
                ["testId"] = context.TestId,
                ["attemptId"] = context.AttemptId,
                ["launchId"] = context.LaunchId,
                ["workerId"] = context.WorkerId,
                ["preparedRevision"] = context.PreparedRevision,
                ["inputIdentity"] = context.InputIdentity,
                ["leaseId"] = context.LeaseId,
                ["osPid"] = System.Environment.ProcessId,
                ["releaseTimeoutSeconds"] = 180,
                ["workTimeoutSeconds"] = 45,
                ["resourceRoot"] = files?.ResourceRoot,
                ["resourcePaths"] = files?.ResourcePaths,
                ["createdId"] = createdId,
                ["checksPassed"] = passed,
                ["expectedChecks"] = ExpectedChecks,
                ["failed"] = error == null ? 0 : 1,
                ["complete"] = complete,
                ["restored"] = files != null && files.MatchesBefore(),
                ["before"] = before,
                ["after"] = files?.Snapshot(),
                ["workSeconds"] = work?.Elapsed.TotalSeconds ?? 0,
                ["error"] = error,
                ["awaitingNativeExit"] = true
            };
            ExecutionEditorWriteFiles.Publish(Path.Combine(context.ArtifactRoot, name), record);
        }

        /// <summary>Stops outstanding frame/gate work if the fixture leaves before normal ApplicationExit.</summary>
        protected override void OnExitTree()
        {
            lifetime.Cancel();
            lifetime.Dispose();
        }
    }
}
