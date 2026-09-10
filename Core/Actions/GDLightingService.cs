using Godot;

namespace Cthangover.Core.Actions
{
	[GlobalClass]
	public partial class GDLightingService : RefCounted
	{
		internal ILightingService Inner;

		public void ClearDepthMap() => Inner.ClearDepthMap();
		public void SetUseTime(bool useTime) => Inner.SetUseTime(useTime);
	}
}
