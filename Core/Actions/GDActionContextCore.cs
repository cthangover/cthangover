namespace Cthangover.Core.Actions;

public class GDActionContextCore
{
	public IActionContext Inner
	{
		get => _inner;
		set
		{
			_inner = value;
			Quests.Inner = value?.Quests;
			Character.Inner = value?.Character;
			Battle.Inner = value?.Battle;
			Lighting.Inner = value?.Lighting;
			Scene.Inner = value?.Scene;
			Inventory.Inner = value?.Inventory;
			ModRegistry.Inner = value?.ModRegistry;
		}
	}

	public string GetParam(string name) => _inner?.GetParam(name);
	public void Log(string category, string message) => _inner?.Log(category, message);
	public void LogWarning(string category, string message) => _inner?.LogWarning(category, message);

	public readonly GDQuestService Quests = new();
	public readonly GDCharacterService Character = new();
	public readonly GDBattleService Battle = new();
	public readonly GDLightingService Lighting = new();
	public readonly GDSceneNodeService Scene = new();
	public readonly Items.GDInventoryService Inventory = new();
	public readonly Mods.GDModRegistryService ModRegistry = new();

	private IActionContext _inner;
}
