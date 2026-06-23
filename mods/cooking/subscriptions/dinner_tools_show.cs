var modPanel = FindNode<Control>("Lastground");
if (modPanel == null)
    return;

var dinner = modPanel.GetNodeOrNull<Mods.Cooking.Rations.RationStatisticsPanel>("RationStatistics");
if (dinner == null)
{
    dinner = new Mods.Cooking.Rations.RationStatisticsPanel();
    dinner.Name = "RationStatistics";
    dinner.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
    dinner.CustomMinimumSize = new Vector2(300, 300);
    modPanel.AddChild(dinner);
    
    dinner.Show();
    modPanel.UpdateMinimumSize();
}
