namespace Cthangover.Core.Bootstrap
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Cthangover.Core.Utils;
    using Godot;

    /// <summary>Lets engine mods finish asynchronous cleanup before SceneTree and its servers are torn down.</summary>
    public static class ApplicationExit
    {
        /// <summary>Handlers start together; Quit waits for all of them. Mods must unregister when leaving the tree.</summary>
        public static event Func<SceneTree, Task> Preparing;
        private static SceneTree runtime;
        private static bool quitting;
        private static int requestedCode;

        /// <summary>Routes the OS window close request through the same cleanup as menus and test drivers.</summary>
        public static void Attach(SceneTree tree)
        {
            if (runtime == tree) return;
            runtime = tree;
            tree.AutoAcceptQuit = false;
            tree.Root.CloseRequested += OnCloseRequested;
        }

        private static void OnCloseRequested() => Quit(runtime);

        /// <summary>Preserves failure exit codes even if another caller requests exit while cleanup is pending.</summary>
        public static async void Quit(SceneTree tree, int exitCode = 0)
        {
            if (quitting)
            {
                if (exitCode != 0) requestedCode = exitCode;
                return;
            }
            quitting = true;
            requestedCode = exitCode;
            var pending = new List<Task>();
            try
            {
                if (Preparing != null)
                    foreach (Func<SceneTree, Task> handler in Preparing.GetInvocationList())
                        pending.Add(handler(tree) ?? Task.CompletedTask);
                await Task.WhenAll(pending);
            }
            catch (Exception error)
            {
                requestedCode = 1;
                GameLogger.Log("BOOT", "Application cleanup failed: " + error.Message, LogLevel.Error);
            }
            tree.Quit(requestedCode);
        }
    }
}
