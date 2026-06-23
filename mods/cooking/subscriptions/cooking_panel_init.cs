var modPanel = FindNode<Control>("ModLastPanel");
if (modPanel == null)
    return;

var panel = modPanel.GetNodeOrNull<Mods.Cooking.Workbench.WorkbenchPanel>("CookingWorkbenchPanel");
if (panel == null)
{
    panel = new Mods.Cooking.Workbench.WorkbenchPanel();
    panel.Name = "CookingWorkbenchPanel";
    panel.Visible = false;
    panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
    modPanel.AddChild(panel);
}
