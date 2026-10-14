using Cthangover.Core.Scenes;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Autotest
{
    /// <summary>
    /// Test stub implementing <see cref="ITscnSceneEntryPoint"/> for integration testing.
    /// Sets a static <see cref="WasCalled"/> flag when <see cref="OnSceneBuilt"/> is invoked,
    /// allowing tests to verify the entry-point lifecycle.
    /// </summary>
    public partial class TscnTestEntryPoint : Node, ITscnSceneEntryPoint
    {
        /// <summary>Set when <see cref="OnSceneBuilt"/> fires; reset via <see cref="Reset"/> between tests.</summary>
        public static bool WasCalled { get; set; }

        /// <summary>
        /// Marks the entry point as invoked and logs the built scene root so the
        /// test log shows which scene reached the callback.
        /// </summary>
        public void OnSceneBuilt(Node sceneRoot)
        {
            WasCalled = true;
            GameLogger.Log("TSCN_TEST", $"TscnTestEntryPoint.OnSceneBuilt: root='{sceneRoot?.Name}'");
        }

        /// <summary>Clears the invocation flag before a new test run.</summary>
        public static void Reset()
        {
            WasCalled = false;
        }
    }
}
