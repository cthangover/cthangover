using Godot;

namespace Cthangover.Core.Battle
{
	/// <summary>
	/// GDScript bridge for <see cref="IWingroundListener"/>.
	/// Holds a reference to a GDScript object and delegates each hook
	/// call through <see cref="GodotObject.HasMethod"/> /
	/// <see cref="GodotObject.Call"/>.
	///
	/// All three hooks are optional — the GDScript mod implements only
	/// the methods it needs. The context is marshalled into
	/// <see cref="GDWingroundContext"/> before the call and synced
	/// back afterward so GDScript mutations propagate to the C# pipe.
	/// </summary>
	public class GDWingroundListener : IWingroundListener
	{
		private readonly GodotObject _instance;

		public string Id { get; }

		/// <summary>
		/// Creates a bridge around a live GDScript object. The object
		/// should be a <c>RefCounted</c> instance created from a
		/// compiled <c>.gd</c> script. <paramref name="instance"/>
		/// is retained by reference — the caller must ensure it
		/// outlives the registry.
		/// </summary>
		public GDWingroundListener(string id, GodotObject instance)
		{
			Id = id;
			_instance = instance;
		}

		public void OnBeforeExpAward(WingroundContext ctx)
		{
			if (!_instance.HasMethod("on_winground_before_exp"))
				return;

			var gdCtx = GDWingroundContext.From(ctx);
			_instance.Call("on_winground_before_exp", gdCtx);
			gdCtx.SyncBack(ctx);
		}

		public void OnAfterLootGenerated(WingroundContext ctx)
		{
			if (!_instance.HasMethod("on_winground_after_loot"))
				return;

			var gdCtx = GDWingroundContext.From(ctx);
			_instance.Call("on_winground_after_loot", gdCtx);
			gdCtx.SyncBack(ctx);
		}

		public void OnBeforeShow(WingroundContext ctx)
		{
			if (!_instance.HasMethod("on_winground_before_show"))
				return;

			var gdCtx = GDWingroundContext.From(ctx);
			_instance.Call("on_winground_before_show", gdCtx);
			gdCtx.SyncBack(ctx);
		}
	}
}
