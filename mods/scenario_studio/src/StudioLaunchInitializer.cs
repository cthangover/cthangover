namespace Cthangover.Mods.ScenarioStudio
{
    using System.Linq;
    using Cthangover.Core.Mods;
    using Cthangover.Core.Utils;
    using Godot;

    /// <summary>
    /// Launch flag <c>--studio</c>: opens the studio dock as soon as the first
    /// game scene is up, so a launcher script (<c>debug_scenario_studio.bat</c>)
    /// lands straight in the studio instead of making the user find its toolbar
    /// button. The opener is added deferred because resources-ready fires
    /// before the shell scene exists.
    /// </summary>
    public class StudioLaunchInitializer : IModInitializer
    {
        private const string Flag = "--studio";

        private static bool openerQueued;

        public void OnModLoaded(string modId) { }

        public void OnModResourcesReady()
        {
            if (openerQueued || !OS.GetCmdlineUserArgs().Contains(Flag))
                return;

            openerQueued = true;
            GameLogger.Log("UI", "Scenario studio: --studio given, the dock opens with the first scene");
            (Godot.Engine.GetMainLoop() as SceneTree)?.Root?.CallDeferred("add_child", new StudioAutoOpenNode());
        }
    }
}
