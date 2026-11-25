namespace Cthangover.Mods.ExecutionEditorWrite
{
    using Cthangover.Core.Mods;
    using Cthangover.Core.Utils;
    using Godot;

    /// <summary>Mounting the inert suite is the only way to queue the actual editor lifecycle driver.</summary>
    public sealed class ExecutionEditorWriteInitializer : IModInitializer
    {
        private static bool queued;

        /// <summary>Individual dependency loads do not start editor work.</summary>
        public void OnModLoaded(string modId)
        {
        }

        /// <summary>Queues once after the suite's dependencies exist; the driver separately waits for completed boot.</summary>
        public void OnModResourcesReady()
        {
            if (!GameEnvironment.IsRuntime || queued)
            {
                return;
            }

            foreach (var argument in OS.GetCmdlineUserArgs())
            {
                if (argument == "--itest=test_execution_editor_write")
                {
                    queued = true;
                    ModRoot.AddDeferred(new ExecutionEditorWriteDriver());
                    return;
                }
            }
        }
    }
}
