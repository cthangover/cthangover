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
        public static bool WasCalled { get; set; }

        public void OnSceneBuilt(Node sceneRoot)
        {
            WasCalled = true;
            GameLogger.Log("TSCN_TEST", $"TscnTestEntryPoint.OnSceneBuilt: root='{sceneRoot?.Name}'");
        }

        public static void Reset()
        {
            WasCalled = false;
        }
    }
}
