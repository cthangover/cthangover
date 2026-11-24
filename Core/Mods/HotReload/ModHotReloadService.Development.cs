namespace Cthangover.Core.Mods.HotReload
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using Cthangover.Core.Bootstrap;
    using Cthangover.Core.Games;
    using Cthangover.Core.Mods.Pipeline;
    using Cthangover.Core.Scenes;
    using Cthangover.Core.Settings;
    using Godot;

    /// <summary>
    /// Development-driven reload path: unlike the file watchers in the other part, this one is invoked
    /// explicitly by the debug host's <c>mod.attach</c>/<c>mod.reload</c> commands, so an agent (or a
    /// developer) can bring one mod — or one changed engine dependency — into the already running process.
    /// <para>
    /// Mechanism and its traps: the target mod is staged with the ordinary pipeline
    /// (<c>PrepareDevelopment</c>), the plan diffs against the live set (load/unload/keep), and unchanged
    /// dependents are kept. A scene that owns nodes of an unloaded assembly cannot be hot-swapped in place,
    /// so the caller asks for a checkpoint: the game saves a hidden slot, shows the transition screen,
    /// applies the plan and reloads the save. That is why an engine-level edit requires
    /// <c>checkpoint=true</c> while attaching a fresh, self-contained mod does not — and why the command
    /// refuses to run while any other transition is in progress.
    /// </para>
    /// </summary>
    public partial class ModHotReloadService
    {
        internal static async Task<ModReloadResult> ReloadDevelopmentAsync(SceneTree tree, string id, bool attach,
            bool checkpoint, CancellationToken token)
        {
            var manager = ModManager.Instance;
            var reloader = ModRegistry.Instance.Reloader;
            if (IsReloadRunning || GameSession.IsSwitching || manager.IsReloading)
                throw new InvalidOperationException("A game transition is already in progress.");

            IsReloadRunning = true;
            ModReloadPreparation prep = null;
            try
            {
                var selection = attach ? GameLaunchArgs.Current.WithAdditionalMod(id) : GameLaunchArgs.Current;
                prep = await Task.Run(() => reloader.PrepareDevelopment(selection, id, attach, token), token);
                token.ThrowIfCancellationRequested();
                if (!prep.Succeeded)
                    return new ModReloadResult(false, null, null, prep.Errors);

                var plan = reloader.Preview(prep);
                var current = tree.CurrentScene;
                var inGame = !string.IsNullOrEmpty(SceneManager.GetCurrentSceneName())
                    && (current?.HasMeta("shell_kind") == true || current?.IsInGroup("main_event_chain") == true);
                if (checkpoint && !inGame)
                    throw new InvalidOperationException("A live gameplay scene is required for a checkpoint.");
                if (!checkpoint && SceneUses(current, plan.Unload))
                    throw new InvalidOperationException("The current scene owns nodes of affected mods. Retry with checkpoint=true.");

                string slot = null;
                if (checkpoint)
                {
                    slot = SaveService.SaveHotReloadCheckpoint();
                    Cthangover.Core.UI.Menu.ModSetTransitionScreen.Show(tree);
                    await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
                }

                var prepared = prep;
                prep = null;
                var result = manager.ApplyReload(prepared, tree);
                await manager.VerifyReloadAsync(result, tree);
                if (slot != null && !ApplicationExit.IsQuitting)
                {
                    if (!GameSession.LoadAndEnter(tree.Root.GetNode("SceneManager"), slot))
                        throw new InvalidOperationException($"Could not restore hot reload checkpoint '{slot}'. The save remains available.");
                    SaveService.DeleteSave(slot);
                }
                return result;
            }
            finally
            {
                if (prep != null)
                    reloader.Discard(prep);
                IsReloadRunning = false;
            }
        }

        private static bool SceneUses(Node scene, IReadOnlyList<string> mods)
        {
            if (scene == null || mods.Count == 0)
                return false;
            var assemblies = new HashSet<Assembly>();
            foreach (var record in ModAssemblyLoader.Records(mods))
                assemblies.UnionWith(record.Assemblies);
            return SceneUses(scene, assemblies);
        }

        private static bool SceneUses(Node node, HashSet<Assembly> assemblies)
        {
            if (assemblies.Contains(node.GetType().Assembly))
                return true;
            return node.GetChildren(includeInternal: true).Any(child => SceneUses(child, assemblies));
        }
    }
}
