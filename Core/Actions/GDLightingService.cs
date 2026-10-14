using Godot;

namespace Cthangover.Core.Actions
{
	/// <summary>
	/// GDScript-facing lighting subsystem exposed on the action context
	/// (<c>ctx.Lighting</c>). Thin delegate over <see cref="ILightingService"/>;
	/// calls forward to the C# context bound by
	/// <see cref="GDActionContextCore.Inner"/>.
	/// </summary>
	[GlobalClass]
	public partial class GDLightingService : RefCounted
	{
		internal ILightingService Inner;

		/// <summary>GDScript: ctx.Lighting.ClearDepthMap() — resets the depth and albedo lighting maps (back to flat lighting).</summary>
		public void ClearDepthMap() => Inner.ClearDepthMap();
		/// <summary>GDScript: ctx.Lighting.SetUseTime(true) — toggles time-of-day-driven light colors and angles.</summary>
		public void SetUseTime(bool useTime) => Inner.SetUseTime(useTime);
	}
}
