using Cthangover.Core.Scenes;
using Cthangover.Core.Actions.Atomic;
using Cthangover.Core.Actions;
using Godot;

namespace Mods.Cooking.Workbench
{
    public class ToggleCookingPanelAction : IScenarioAction
    {
        public string Name => "toggle_cooking_workbench";

        public void Run(IActionContext ctx)
        {
            var root = SceneContextNode.FindNode<Control>("ModLastPanel");
            if (root == null)
                return;

            var panel = root.GetNodeOrNull<WorkbenchPanel>("CookingWorkbenchPanel");
            panel?.Switch();
        }
    }
}
