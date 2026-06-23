var modPanel = FindNode<Control>("Lastground");
if (modPanel == null)
    return;

var dinner = modPanel.GetNodeOrNull<Mods.Cooking.Rations.RationStatisticsPanel>("RationStatistics");
if (dinner != null)
    dinner.QueueFree();
