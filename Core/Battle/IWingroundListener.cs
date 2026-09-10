namespace Cthangover.Core.Battle
{
	/// <summary>
	/// Extension point for mods to inspect and mutate victory-screen
	/// data. Implementations are auto-discovered via
	/// <see cref="WingroundListenerRegistry.RegisterAssembly"/> (C#)
	/// or detected by method signature in
	/// <see cref="Mods.ModGDScriptLoader"/> (GDScript).
	///
	/// The three hooks fire in order during
	/// <see cref="WingroundBehaviour.Show"/>:
	/// <list type="number">
	/// <item><see cref="OnBeforeExpAward"/> — EXP data is available
	/// but has not yet been applied to characters.</item>
	/// <item><see cref="OnAfterLootGenerated"/> — loot has been
	/// rolled from enemies but has not yet been added to inventory.</item>
	/// <item><see cref="OnBeforeShow"/> — all processing is done;
	/// final chance to mutate data before the UI appears.</item>
	/// </list>
	/// The same <see cref="WingroundContext"/> instance is passed to
	/// all listeners across all three phases, so mutations from earlier
	/// hooks are visible to later hooks and to subsequent listeners.
	/// </summary>
	public interface IWingroundListener
	{
		/// <summary>
		/// Unique identifier for this listener. Used in logs and for
		/// cross-mod dependency ordering. Convention:
		/// <c>"mod_id.listener_name"</c>.
		/// </summary>
		string Id { get; }

		/// <summary>
		/// Called before EXP is awarded to surviving characters.
		/// <see cref="WingroundContext.PlayerExpGained"/> and
		/// <see cref="WingroundContext.DefeatedEnemies"/> are
		/// populated; <see cref="WingroundContext.GeneratedLoot"/>
		/// is empty at this stage.
		/// </summary>
		void OnBeforeExpAward(WingroundContext ctx);

		/// <summary>
		/// Called after loot has been generated from
		/// <see cref="WingroundContext.DefeatedEnemies"/> but before
		/// the items are added to the player's inventory.
		/// <see cref="WingroundContext.GeneratedLoot"/> is populated
		/// and mutable — adding, removing, or modifying entries here
		/// directly affects the inventory outcome.
		/// </summary>
		void OnAfterLootGenerated(WingroundContext ctx);

		/// <summary>
		/// Called after EXP, loot, inventory, and recruitment have
		/// all been processed, immediately before the victory UI
		/// is shown (<c>base.Show()</c>). Use for post-processing
		/// or cleanup that must happen before the player sees the
		/// screen.
		/// </summary>
		void OnBeforeShow(WingroundContext ctx);
	}
}
