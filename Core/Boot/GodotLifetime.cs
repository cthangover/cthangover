namespace Cthangover.Core.Bootstrap
{
    using System;
    using Godot;

    /// <summary>Coordinates CLR collection with the native lifetime of the Godot 4.6.3 runtime.</summary>
    public static class GodotLifetime
    {
        /// <summary>Stops scene-tree animations while their targets and the native engine are still alive.</summary>
        public static void StopAnimations(SceneTree tree)
        {
            using var animations = (Godot.Collections.Array)tree.GetProcessedTweens();
            foreach (var value in animations)
            {
                using (value)
                    value.As<Tween>()?.Kill();
            }
        }

        /// <summary>
        /// Completes queued native disposal before SceneTree.Quit tears down the scripting
        /// language. Called after background producers and asynchronous owners have stopped.
        /// The second pass collects wrappers made unreachable by the first finalizer pass.
        /// </summary>
        public static void DrainFinalizers()
        {
            GC.WaitForPendingFinalizers();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
    }
}
