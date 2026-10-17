using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cthangover.Core.Bootstrap;
using Cthangover.Core.Events;
using Cthangover.Core.Games;
using Cthangover.Core.Mods;
using Cthangover.Core.Mods.HotReload;
using Cthangover.Core.Utils;
using Godot;

namespace Mods.HotReloadTest
{
    /// <summary>
    /// Hot reload in the running front-end, driven through the real file
    /// watcher. Two probe mods are written into <c>user://mods</c>: a library
    /// returning a version string and an app mod (depending on it) whose
    /// initializer puts a node with that string into <c>/root</c>.
    /// <list type="number">
    /// <item>v1 is loaded by adding the probes to the selection and writing the files.</item>
    /// <item>Editing the library to v2 reloads both probes (the dependent
    /// cascades), the old contexts are collected, the front-end stays.</item>
    /// <item>A compile error in the library is rejected: v2 keeps running.</item>
    /// <item>Fixing it (v3) reloads again.</item>
    /// <item>Dropping the probes from the selection unloads them and their node.</item>
    /// </list>
    /// Reload outcomes are read from <see cref="ModSetReloadedEvent"/>s, since
    /// the watcher may add no-op reloads of its own. Prints
    /// <c>[TEST_RESULT] name=hot_reload</c>.
    /// </summary>
    public partial class HotReloadTestDriverNode : Node
    {
        public const string Flag = "--itest=hot_reload";

        private const string LibId = "hot_reload_probe_lib";
        private const string AppId = "hot_reload_probe_app";
        private const string ProbeNodeName = "HotReloadProbeNode";
        private const float TimeoutSeconds = 300f;

        private readonly List<string> failures = new();
        private readonly List<ModSetReloadedEvent> reloads = new();
        private int passed;
        private int step;
        private double elapsed;
        private double stepAt;
        private bool done;
        private GameLaunchArgs startSelection;
        private string modsRoot;

        public override void _Ready()
        {
            ProcessMode = ProcessModeEnum.Always;
            ModEventBus.Subscribe<ModSetReloadedEvent>(OnReloaded);
            modsRoot = ProjectSettings.GlobalizePath("user://mods");
        }

        public override void _ExitTree()
        {
            ModEventBus.Unsubscribe<ModSetReloadedEvent>(OnReloaded);
        }

        private void OnReloaded(ModSetReloadedEvent e) => reloads.Add(e);

        public override void _Process(double delta)
        {
            if (done)
                return;

            elapsed += delta;
            if (elapsed > TimeoutSeconds)
            {
                failures.Add($"timeout at step {step} (probe='{ProbeValue()}', reloads={reloads.Count})");
                Finish();
                return;
            }

            if (elapsed - stepAt < 0.25)
                return;
            stepAt = elapsed;

            try
            {
                RunStep();
            }
            catch (Exception ex)
            {
                failures.Add($"step {step}: {ex.GetType().Name}: {ex.Message}");
                Finish();
            }
        }

        private static bool Busy => GameSession.IsSwitching || ModManager.Instance.IsReloading || ModHotReloadService.IsReloadRunning;

        private void RunStep()
        {
            switch (step)
            {
                case 0:
                    if (!Boot.IsReady)
                        return;
                    // First launch shows the launcher screen; go on like a player.
                    if (GetTree().CurrentScene?.FindChild("LauncherPlay", true, false) is Button launcherPlay)
                    {
                        launcherPlay.EmitSignal(BaseButton.SignalName.Pressed);
                        return;
                    }
                    if (GetTree().CurrentScene?.FindChild("play", true, false) == null)
                        return;
                    Check("hot reload watcher is installed", ModHotReloadService.Instance != null);
                    startSelection = GameLaunchArgs.Current;
                    GameLaunchArgs.SetCurrent(GameLaunchArgs.Parse(startSelection.UserArgs.Concat(new[] { GameLaunchArgs.WithModsPrefix + AppId })));
                    reloads.Clear();
                    WriteProbes("v1");
                    break;

                case 1:
                    if (Busy || ProbeValue() != "v1")
                        return;
                    Check("probes loaded by the watcher", AnyApplied(LoadedBoth));
                    CheckNoLeaks("v1");
                    reloads.Clear();
                    WriteLib("v2", compiles: true);
                    break;

                case 2:
                    if (Busy || ProbeValue() != "v2")
                        return;
                    Check("editing the library reloads it and its dependent", AnyApplied(UnloadedAndLoadedBoth));
                    Check("the front-end stays loaded", !AnyApplied(UnloadedBaseMenu));
                    CheckNoLeaks("v2");
                    reloads.Clear();
                    WriteLib("v2", compiles: false);
                    break;

                case 3:
                    if (Busy || !reloads.Any(IsRejectedForLib))
                        return;
                    Check("a compile error rejects the reload", true);
                    Check("the previous code keeps running", ProbeValue() == "v2");
                    reloads.Clear();
                    WriteLib("v3", compiles: true);
                    break;

                case 4:
                    if (Busy || ProbeValue() != "v3")
                        return;
                    Check("fixing the error reloads again", AnyApplied(LoadedBoth));
                    CheckNoLeaks("v3");
                    reloads.Clear();
                    GameLaunchArgs.SetCurrent(startSelection);
                    StartManualReload();
                    break;

                case 5:
                    if (Busy || ProbeValue() != null || !AnyApplied(UnloadedBoth))
                        return;
                    Check("dropping the probes unloads them and their node", true);
                    CheckNoLeaks("removal");
                    DeleteProbes();
                    Finish();
                    return;
            }

            step++;
        }

        private static async void StartManualReload() => await ModHotReloadService.ReloadNowAsync();

        private string ProbeValue()
        {
            var node = GetTree().Root.GetNodeOrNull(ProbeNodeName);
            return node != null && IsInstanceValid(node) && node.HasMeta("value") ? node.GetMeta("value").AsString() : null;
        }

        private bool AnyApplied(Func<ModSetReloadedEvent, bool> predicate)
        {
            foreach (var reload in reloads)
            {
                if (reload.Applied && predicate(reload))
                    return true;
            }
            return false;
        }

        private static bool LoadedBoth(ModSetReloadedEvent e) => e.Loaded.Contains(LibId) && e.Loaded.Contains(AppId);

        private static bool UnloadedBoth(ModSetReloadedEvent e) => e.Unloaded.Contains(LibId) && e.Unloaded.Contains(AppId);

        private static bool UnloadedAndLoadedBoth(ModSetReloadedEvent e) => UnloadedBoth(e) && LoadedBoth(e);

        private static bool UnloadedBaseMenu(ModSetReloadedEvent e) => e.Unloaded.Contains("base_menu");

        private static bool IsRejectedForLib(ModSetReloadedEvent e) => !e.Applied && e.Errors.Any(IsLibError);

        private static bool IsLibError(string error) => error.Contains(LibId);

        private void CheckNoLeaks(string stage)
        {
            var leaked = new List<string>();
            foreach (var reload in reloads)
                leaked.AddRange(reload.Leaked);
            Check($"every released context collected ({stage})", leaked.Count == 0);
            if (leaked.Count > 0)
                GameLogger.Log("TEST", $"HotReloadTest: leaked: {string.Join(", ", leaked)}", LogLevel.Error);
        }

        private void WriteProbes(string version)
        {
            var app = Path.Combine(modsRoot, AppId);
            Directory.CreateDirectory(Path.Combine(app, "src"));
            File.WriteAllText(Path.Combine(app, "manifest.json"),
                "{ \"id\": \"" + AppId + "\", \"name\": \"Hot reload probe app\", \"version\": \"1.0.0\", \"author\": \"test\", " +
                "\"sources\": [\"src/*.cs\"], \"depends\": [\"" + LibId + ">=1\"] }");
            File.WriteAllText(Path.Combine(app, "src", "ProbeApp.cs"), ProbeAppSource);
            WriteLib(version, compiles: true);
        }

        private void WriteLib(string version, bool compiles)
        {
            var lib = Path.Combine(modsRoot, LibId);
            Directory.CreateDirectory(Path.Combine(lib, "src"));
            File.WriteAllText(Path.Combine(lib, "manifest.json"),
                "{ \"id\": \"" + LibId + "\", \"name\": \"Hot reload probe library\", \"version\": \"1.0.0\", \"author\": \"test\", \"sources\": [\"src/*.cs\"] }");
            var body = compiles ? "\"" + version + "\";" : "\"" + version + "\"";
            File.WriteAllText(Path.Combine(lib, "src", "ProbeLib.cs"),
                "namespace HotReloadProbe\n{\n    public static class ProbeLib\n    {\n        public static string Value() => " + body + "\n    }\n}\n");
        }

        private void DeleteProbes()
        {
            foreach (var id in new[] { LibId, AppId })
            {
                var dir = Path.Combine(modsRoot, id);
                if (Directory.Exists(dir))
                    Directory.Delete(dir, recursive: true);
            }
        }

        private const string ProbeAppSource = @"using Godot;

namespace HotReloadProbeApp
{
    public partial class ProbeNode : Node
    {
    }

    public class ProbeAppInitializer : Cthangover.Core.Mods.IModInitializer
    {
        public void OnModLoaded(string modId) { }

        public void OnModResourcesReady()
        {
            var root = (Engine.GetMainLoop() as SceneTree)?.Root;
            if (root == null)
                return;
            root.GetNodeOrNull(""HotReloadProbeNode"")?.Free();
            var node = new ProbeNode { Name = ""HotReloadProbeNode"" };
            node.SetMeta(""value"", HotReloadProbe.ProbeLib.Value());
            root.AddChild(node);
        }
    }
}
";

        private void Check(string name, bool ok)
        {
            if (ok)
            {
                passed++;
                GameLogger.Log("TEST", $"HotReloadTest: [OK] {name}");
            }
            else
            {
                failures.Add(name);
                GameLogger.Log("TEST", $"HotReloadTest: [FAIL] {name}", LogLevel.Error);
            }
        }

        private void Finish()
        {
            if (done)
                return;
            done = true;
            foreach (var failure in failures)
                GameLogger.Log("TEST", $"HotReloadTest: failed: {failure}");
            GameLogger.Log("TEST", $"[TEST_RESULT] name=hot_reload passed={passed} failed={failures.Count}");
            GetTree().Quit(failures.Count == 0 ? 0 : 1);
        }
    }
}
