using System.Collections.Generic;
using System.Linq;
using Cthangover.Core.Battle.Actions;
using Cthangover.Core.Characters;
using Cthangover.Core.Factories.Impls;
using Cthangover.Core.Items;
using Godot;

namespace Cthangover.Core.Battle
{
	/// <summary>
	/// GDScript-facing snapshot of a <see cref="WingroundContext"/>
	/// carrying all the data a GDScript mod needs during the three
	/// winground hooks. Core battle stat fields are exposed through
	/// <see cref="GDCharacter"/> DTOs; loot is exposed as lightweight
	/// <see cref="GDItemContainer"/> entries keyed by item factory ID.
	/// All collections are mutable — mods write back to them and the
	/// changes propagate to the C#-side context via
	/// <see cref="GDWingroundListener.SyncBack"/>.
	/// </summary>
	[GlobalClass]
	public partial class GDWingroundContext : RefCounted
	{
		/// <summary>
		/// Per-character EXP earned, keyed by <see cref="GDCharacter.Id"/>.
		/// Mods can read and write values; changes are synced back to
		/// <see cref="WingroundContext.PlayerExpGained"/>.
		/// </summary>
		public Godot.Collections.Dictionary<string, int> ExpGained = new();

		/// <summary>
		/// Defeated enemies as <see cref="GDCharacter"/> snapshots.
		/// The array references the same <see cref="Character"/>
		/// objects that drive loot generation — modifications to
		/// element order or content are not synced back to the C#
		/// list because GDScript code works with the DTOs directly.
		/// </summary>
		public Godot.Collections.Array<GDCharacter> DefeatedEnemies = new();

		/// <summary>
		/// Generated loot entries. Mods can append, remove, or
		/// modify entries here; changes are synced back to
		/// <see cref="WingroundContext.GeneratedLoot"/> before the
		/// items are committed to inventory.
		/// </summary>
		public Godot.Collections.Array<GDItemContainer> GeneratedLoot = new();

		/// <summary>
		/// Scene name the player returns to after the victory screen
		/// (e.g. <c>"town_entry"</c>). Pulled from
		/// <see cref="Settings.BattleData.ReturnScene"/>.
		/// </summary>
		public string ReturnScene;

		/// <summary>
		/// Populates this GDScript context from a C#
		/// <see cref="WingroundContext"/>. Called by
		/// <see cref="GDWingroundListener"/> before each hook.
		/// </summary>
		public static GDWingroundContext From(WingroundContext ctx)
		{
			var gd = new GDWingroundContext();

			if (ctx == null)
				return gd;

			if (ctx.PlayerExpGained != null)
			{
				foreach (var kvp in ctx.PlayerExpGained)
					gd.ExpGained[kvp.Key] = kvp.Value;
			}

			if (ctx.DefeatedEnemies != null)
				gd.DefeatedEnemies = new Godot.Collections.Array<GDCharacter>(
					ctx.DefeatedEnemies.Select(GDCharacter.From));

			if (ctx.GeneratedLoot != null)
			{
				var arr = new Godot.Collections.Array<GDItemContainer>();
				foreach (var item in ctx.GeneratedLoot)
				{
					arr.Add(new GDItemContainer
					{
						ItemId = item.Item?.ID ?? "",
						Count = item.Count
					});
				}
				gd.GeneratedLoot = arr;
			}

			gd.ReturnScene = ctx.BattleData?.ReturnScene ?? "";

			return gd;
		}

		/// <summary>
		/// Writes mutations made to this GDScript context back into
		/// the C# <see cref="WingroundContext"/>. Called by
		/// <see cref="GDWingroundListener"/> after each hook so that
		/// changes from GDScript propagate to the victory pipeline.
		/// Only <see cref="ExpGained"/> and
		/// <see cref="GeneratedLoot"/> are synced —
		/// <see cref="DefeatedEnemies"/> DTOs are snapshots and
		/// mutations are not reflected in the original
		/// <see cref="Character"/> list.
		/// </summary>
		public void SyncBack(WingroundContext ctx)
		{
			if (ctx == null)
				return;

			if (ExpGained != null && ctx.PlayerExpGained != null)
			{
				ctx.PlayerExpGained.Clear();
				foreach (var kvp in ExpGained)
					ctx.PlayerExpGained[kvp.Key] = kvp.Value;
			}

			if (GeneratedLoot != null && ctx.GeneratedLoot != null)
			{
				ctx.GeneratedLoot.Clear();
				foreach (var gdItem in GeneratedLoot)
				{
					var item = string.IsNullOrEmpty(gdItem.ItemId)
						? null
						: ItemFactory.Instance.Get(gdItem.ItemId);
					ctx.GeneratedLoot.Add(new ItemContainer
					{
						Item = item,
						Count = gdItem.Count
					});
				}
			}
		}
	}
}
