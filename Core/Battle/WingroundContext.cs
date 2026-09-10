using System.Collections.Generic;
using Cthangover.Core.Characters;
using Cthangover.Core.Items;
using Cthangover.Core.Settings;

namespace Cthangover.Core.Battle
{
	/// <summary>
	/// Mutable context passed to <see cref="IWingroundListener"/> hooks
	/// during the victory-screen pipeline. Listeners receive the same
	/// instance across all three phases (before EXP, after loot, before
	/// show) and can mutate any field — including removing enemies,
	/// adjusting earned EXP per character, or modifying the generated
	/// loot list. The <see cref="WingroundBehaviour"/> reads these
	/// fields after each hook, so changes take effect immediately.
	/// </summary>
	public class WingroundContext
	{
		/// <summary>
		/// Per-character EXP earned during this battle, keyed by
		/// <see cref="Character.ID"/>. Populated from
		/// <see cref="BattleSceneContext.PlayerExpGained"/> before the
		/// first hook and consumed by <see cref="WingroundBehaviour"/>
		/// to award EXP to <see cref="CharacterData"/>.
		/// </summary>
		public Dictionary<string, int> PlayerExpGained { get; set; } = new();

		/// <summary>
		/// Enemies defeated during this battle. Used by the victory
		/// screen for loot generation and recruitment rolls. Null
		/// entries are skipped by <see cref="WingroundBehaviour"/>.
		/// </summary>
		public List<Character> DefeatedEnemies { get; set; } = new();

		/// <summary>
		/// Loot items generated from <see cref="DefeatedEnemies"/>.
		/// Filled by <see cref="WingroundBehaviour"/> between the
		/// "before EXP" and "after loot" hooks. Listeners can
		/// add, remove, or modify entries before the items are
		/// committed to inventory.
		/// </summary>
		public List<ItemContainer> GeneratedLoot { get; set; } = new();

		/// <summary>
		/// The <see cref="BattleData"/> that started this battle.
		/// Read-only reference — do not mutate the object itself (it
		/// is shared with the quest system) but its fields such as
		/// <c>ReturnScene</c> are writable.
		/// </summary>
		public BattleData BattleData { get; set; }
	}
}
