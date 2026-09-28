using Cthangover.Core.Events;
using Godot;

namespace Cthangover.Core.Actions;

[GlobalClass]
public partial class GDActionContext : RefCounted
{
	internal GDActionContextCore Core = new();

	public GDQuestService Quests => Core.Quests;
	public GDCharacterService Character => Core.Character;
	public GDLightingService Lighting => Core.Lighting;
	public GDSceneNodeService Scene => Core.Scene;
	public Items.GDInventoryService Inventory => Core.Inventory;
	public Mods.GDModRegistryService ModRegistry => Core.ModRegistry;

	public string GetParam(string name) => Core.GetParam(name);
	public void Log(string category, string message) => Core.Log(category, message);
	public void LogWarning(string category, string message) => Core.LogWarning(category, message);

	/// <summary>
	/// GDScript: ctx.PublishEvent("cooking_meal_ready", {"dish": "wolf_stew"})
	/// Publishes an event to the global bus for inter-mod communication.
	/// </summary>
	public void PublishEvent(string eventKey, Godot.Collections.Dictionary data)
	{
		ModEventBus.Instance?.Publish(eventKey, data);
	}
}
