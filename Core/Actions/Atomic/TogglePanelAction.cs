using Godot;

namespace Cthangover.Core.Actions.Atomic
{
    public class TogglePanelAction : IScenarioAction
    {
        public string Name => "ui.toggle_panel";

        public void Run(IActionContext ctx)
        {
            var name = ctx.GetParam("name");

            if (string.IsNullOrEmpty(name))
            {
                ctx.Log("EVENT", "TogglePanelAction: missing 'name' variable");
                return;
            }

            var panel = ctx.Scene.Find<Control>(name);
            if (panel != null)
            {
                panel.Visible = !panel.Visible;
                ctx.Log("WIDGET", $"TogglePanelAction: toggled '{name}' visibility to {panel.Visible}");
            }
            else
            {
                ctx.Log("EVENT", $"TogglePanelAction: panel '{name}' not found");
            }
        }
    }
}
