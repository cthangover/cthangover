using Godot;

namespace Cthangover.Core.Actions
{
	[GlobalClass]
	public partial class GDSceneNodeService : RefCounted
	{
		internal ISceneNodeService Inner;

		public void Instantiate(string scenePath, string nodeName) => Inner.Instantiate(scenePath, nodeName);
		public void Remove(string nodeName) => Inner.Remove(nodeName);
	}
}
