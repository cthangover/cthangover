using System;
using System.Collections.Generic;
using System.Linq;
using Cthangover.Core.Audio;
using Cthangover.Core.Factories.Impls;
using Cthangover.Core.Items;
using Cthangover.Core.Mods;
using Cthangover.Core.Relationship;
using Cthangover.Core.Scenes;
using Cthangover.Core.Settings;
using Cthangover.Core.UI.Base.Lists;
using Cthangover.Core.Characters;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Battle
{
    /// <summary>
    /// Victory screen widget (extends TransitionWidget for fade-in).
    /// On Show it: awards tracked EXP to each surviving character in the
    /// battle set, builds a 4-slot EXP report card row (with placeholders
    /// for missing slots to keep layout stable), rolls loot from defeated
    /// enemies using per-item probability/range, lays out loot in a grid
    /// whose cell size is computed from the container width and
    /// lootAspectHeight, adds items to inventory, and processes character
    /// recruitment via RecruitBehaviourRegistry. A "return" button sets
    /// the pending scene and reloads BaseScene — the actual destination
    /// scene (town_entry or the quest's ReturnScene) is injected by
    /// the scene manager.
    /// </summary>
	public partial class WingroundBehaviour : TransitionWidget
	{
		[Export] private Label txtInfo;
		[Export] private Label lootTitle;
		[Export] private Container charExpContainer;
		[Export] private PackedScene expReportCardPrefab;
		[Export] private Control lootContent;
		[Export] private PackedScene lootReportItemPrefab;
		[Export] private int lootColumnCount = 5;
		[Export] private float lootAspectHeight = 1.2f;
		[Export] private Vector2 lootCellPadding = new(4f, 4f);
		[Export] private float lootBorderLeft = 8f;
		[Export] private float lootBorderRight = 8f;
		[Export] private float lootBorderTop = 8f;
		[Export] private float lootBorderBottom = 8f;

		private BattleData data;

        /// <summary>
        /// Victory screen entry point. Awards tracked EXP to each
        /// surviving character, builds the EXP report row (4 slots,
        /// placeholders for missing characters), generates loot from
        /// defeated enemies, lays out the loot grid, adds items to
        /// inventory, and processes character recruitment rolls.
        /// </summary>
        public override void Show()
		{
			lootTitle.Text = TranslationServer.Translate("ui/battle/victory/loot_title");
			
			GameLogger.Log("BATTLE", $"Winground.Show() ENTER: Visible={Visible}, GodotVisible={base.Visible}, txtInfo={(txtInfo != null ? "found" : "NULL")}", LogLevel.Debug);

			var expTracking = BattleSceneContext.Instance?.PlayerExpGained;

			GameLogger.Log("BATTLE_UI", $"Winground.Show: expTracking={(expTracking != null ? $"found ({expTracking.Count} entries)" : "NULL")}", LogLevel.Error);
			if (expTracking != null)
			{
				foreach (var kvp in expTracking)
					GameLogger.Log("BATTLE_UI", $"  expTracking['{kvp.Key}'] = {kvp.Value}", LogLevel.Debug);
			}
			GameLogger.Log("BATTLE_UI", $"Winground.Show: BattleSet count={GameData.Instance.Runtime.CharacterData.BattleSet.Count}", LogLevel.Debug);

			var audioService = GetNode<AudioService>("/root/AudioService");
			audioService?.PlaySound("battle/winground", SoundType.UI);

			var bg = GetNodeOrNull<TextureRect>("TransitionBg");
			GameLogger.Log("BATTLE", $"Winground.Show: TransitionBg={(bg != null ? "found" : "NULL")}, texture={(bg?.Texture != null ? "has" : "null")}", LogLevel.Error);

			if (bg != null && bg.Texture == null)
			{
				var tex = ModManager.Instance.ResolveTexture("winground");
				GameLogger.Log("BATTLE", $"Winground.Show: ResolveTexture('winground') = {(tex != null ? "loaded" : "NULL")}", LogLevel.Error);
				bg.Texture = tex;
			}

			data = GameData.Instance.Runtime.BattleData;
			GameLogger.Log("BATTLE_UI", $"Winground.Show: BattleData={(data != null ? "found" : "NULL")}", LogLevel.Error);

			foreach (var characterType in GameData.Instance.Runtime.CharacterData.BattleSet)
			{
				if (!GameData.Instance.Runtime.CharacterData.Characters.TryGetValue(characterType, out var info))
				{
					GameLogger.Log("BATTLE_UI", $"Winground.Show: characterType='{characterType}' NOT FOUND in Characters dict", LogLevel.Error);
					continue;
				}

				var earnedExp = 0;
				if (expTracking != null && info?.ID != null)
					expTracking.TryGetValue(info.ID, out earnedExp);

				GameLogger.Log("BATTLE_UI", $"Winground.Show: char='{characterType}' id='{info?.ID}' oldExp={info.Exp} earnedExp={earnedExp} newExp={info.Exp + earnedExp}", LogLevel.Debug);

				info.Exp += earnedExp;
			}

			BuildCharExpDisplay();

			var defeatedEnemies = BattleSceneContext.Instance?.DefeatedEnemies;
			if (defeatedEnemies != null && defeatedEnemies.Count > 0)
			{
				var loot = GenerateLoot(defeatedEnemies);
				BuildLootDisplay(loot);
				AddLootToInventory(loot);
				
				ProcessRecruitment(defeatedEnemies);
			}

			base.Show();
			GameLogger.Log("BATTLE", $"Winground.Show() EXIT: Visible={Visible}, GodotVisible={base.Visible}", LogLevel.Debug);
		}

		private const int REPORT_SLOT_COUNT = 4;

		private void BuildCharExpDisplay()
		{
			var container = charExpContainer ?? GetNodeOrNull<Container>("Content/CharExpContainer");
			GameLogger.Log("BATTLE_UI", $"BuildCharExpDisplay: charExpContainer export={(charExpContainer != null ? "found" : "NULL")}, GetNodeOrNull fallback={(container != null ? "found" : "NULL")}, expReportCardPrefab={(expReportCardPrefab != null ? "found" : "NULL")}", LogLevel.Error);

			if (container == null || expReportCardPrefab == null)
			{
				GameLogger.Log("BATTLE_UI", "BuildCharExpDisplay: EARLY RETURN — container or prefab is null", LogLevel.Error);
				return;
			}

			foreach (var child in container.GetChildren())
				child.QueueFree();

			var expTracking = BattleSceneContext.Instance?.PlayerExpGained;
			var charData = GameData.Instance.Runtime.CharacterData;
			
			GameLogger.Log("BATTLE_UI", $"BuildCharExpDisplay: expTracking={(expTracking != null ? $"found ({expTracking.Count} entries)" : "NULL")}, BattleSet count={charData.BattleSet.Count}", LogLevel.Error);

			var battleSetList = charData.BattleSet.ToList();

			for (var i = 0; i < REPORT_SLOT_COUNT; i++)
			{
				if (i < battleSetList.Count)
				{
					var characterType = battleSetList[i];
					if (!charData.Characters.TryGetValue(characterType, out var info))
					{
						GameLogger.Log("BATTLE_UI", $"BuildCharExpDisplay: characterType='{characterType}' NOT FOUND in Characters dict", LogLevel.Error);
						AddPlaceholderSlot(container, i);
						continue;
					}

					var earnedExp = 0;
					expTracking?.TryGetValue(info.ID, out earnedExp);

					var cardTemplate = CharacterFactory.Instance.Get(info.ID);
					var name = cardTemplate?.Name != null
						? TranslationServer.Translate(cardTemplate.Name).ToString()
						: info.ID;

					var reportCard = expReportCardPrefab.Instantiate<ExpReportCard>();
					reportCard.Setup(cardTemplate?.Image, name, earnedExp);
					container.AddChild(reportCard);
					
					GameLogger.Log("BATTLE_UI", $"BuildCharExpDisplay: added reportCard for '{characterType}', id='{info.ID}', name='{name}', earnedExp={earnedExp}, cardTemplate={(cardTemplate != null ? "found" : "NULL")}, icon={(cardTemplate?.Image != null ? "has" : "null")}", LogLevel.Error);
				}
				else
				{
					AddPlaceholderSlot(container, i);
				}
			}

			GameLogger.Log("BATTLE_UI", $"BuildCharExpDisplay: finished, container now has {container.GetChildCount()} children", LogLevel.Debug);
		}

		private static void AddPlaceholderSlot(Container container, int slotIndex)
		{
			var placeholder = new Control();
			placeholder.CustomMinimumSize = new Vector2(130, 130);
			placeholder.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			placeholder.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
			placeholder.MouseFilter = MouseFilterEnum.Ignore;
			container.AddChild(placeholder);

			GameLogger.Log("BATTLE_UI", $"BuildCharExpDisplay: added placeholder slot {slotIndex}", LogLevel.Debug);
		}

		private static readonly Random random = new();

		private List<ItemContainer> GenerateLoot(List<Character> enemies)
		{
			var loot = new List<ItemContainer>();

			foreach (var enemy in enemies)
			{
				if (enemy?.Loot == null || enemy.Loot.Count == 0)
					continue;

				foreach (var entry in enemy.Loot)
				{
					if (string.IsNullOrWhiteSpace(entry.ItemId))
						continue;

					var probability = Math.Clamp(entry.Probability, 0, 100);
					if (probability <= 0)
						continue;
					if (probability < 100 && random.Next(1, 101) > probability)
						continue;

					var count = entry.MinCount == entry.MaxCount
						? entry.MinCount
						: random.Next(entry.MinCount, entry.MaxCount + 1);

					if (count > 0)
						TryAddLootItem(loot, entry.ItemId, count);
				}
			}

			return loot;
		}

		private static void TryAddLootItem(List<ItemContainer> loot, string itemId, int count)
		{
			var existing = loot.Find(c => c.Item?.ID == itemId);
			if (existing != null)
			{
				existing.Count += count;
			}
			else
			{
				var item = ItemFactory.Instance.Get(itemId);
				if (item != null)
					loot.Add(new ItemContainer { Item = item, Count = count });
			}
		}

		private void BuildLootDisplay(List<ItemContainer> loot)
		{
			var content = lootContent ?? GetNodeOrNull<Control>("Content/LootScrollContainer/LootContent");
			if (content == null || lootReportItemPrefab == null)
				return;

			foreach (var child in content.GetChildren())
				child.QueueFree();

			if (loot.Count == 0)
				return;

			var scrollContainer = content.GetParent() as ScrollContainer;
			if (scrollContainer != null)
			{
				content.CustomMinimumSize = new Vector2(scrollContainer.Size.X, 0);
				content.Size = new Vector2(scrollContainer.Size.X, content.Size.Y);
			}

			var effectiveWidth = content.Size.X - lootBorderLeft - lootBorderRight;
			var cellWidth = (effectiveWidth - (lootColumnCount - 1) * lootCellPadding.X) / lootColumnCount;
			var cellHeight = cellWidth * lootAspectHeight;

			var rows = Mathf.CeilToInt((float)loot.Count / lootColumnCount);
			var totalHeight = lootBorderTop + lootBorderBottom + rows * cellHeight + (rows - 1) * lootCellPadding.Y;
			content.CustomMinimumSize = new Vector2(content.CustomMinimumSize.X, totalHeight);

			for (int i = 0; i < loot.Count; i++)
			{
				var itemContainer = loot[i];
				var item = itemContainer.Item;
				if (item == null)
					continue;

				var localizedName = TranslationServer.Translate(item.Name).ToString();

				var reportItem = lootReportItemPrefab.Instantiate<LootReportItem>();
				reportItem.Setup(item.Sprite, localizedName, itemContainer.Count);

				var col = i % lootColumnCount;
				var row = i / lootColumnCount;
				reportItem.Position = new Vector2(
					lootBorderLeft + col * (cellWidth + lootCellPadding.X),
					lootBorderTop + row * (cellHeight + lootCellPadding.Y)
				);
				reportItem.Size = new Vector2(cellWidth, cellHeight);

				content.AddChild(reportItem);
			}
		}

		private static void AddLootToInventory(List<ItemContainer> loot)
		{
			var inventory = GameData.Instance.Runtime.Inventory;
			foreach (var itemContainer in loot)
			{
				if (itemContainer.Item != null)
					inventory.Add(itemContainer.Item, itemContainer.Count);
			}
		}

		private void ProcessRecruitment(List<Character> enemies)
		{
			var runtime = GameData.Instance.Runtime;
			var registry = RecruitBehaviourRegistry.Instance;

			foreach (var enemy in enemies)
			{
				if (enemy == null || enemy.RecruitmentChance <= 0)
					continue;
				if (!registry.CanRecruit(enemy, runtime))
					continue;

				var roll = random.Next(1, 101);
				if (roll > enemy.RecruitmentChance)
					continue;

				runtime.RecruitingData.Add(enemy.ID, enemy.ID);
			}
		}

		private void ToReturnScene()
		{
			if (data == null)
			{
				GameLogger.Log("BATTLE", "battle data is null!", LogLevel.Error);
			}

			if (data?.Quest != null && data?.NewTag != null)
				data.Quest.AddTag(data.NewTag);
			var returnScene = data?.ReturnScene ?? "town_entry";
			GameData.Instance.Runtime.BattleData = null;

			var sceneService = GetNode<GodotSceneService>("/root/GodotSceneService");
			var sceneManager = GetNode<SceneManager>("/root/SceneManager");
			if (sceneManager != null)
				sceneManager.PendingSceneName = returnScene;
			sceneService?.LoadScene("res://Scenes/BaseScene.tscn");
		}
	}
}
