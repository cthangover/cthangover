using Cthangover.Core.Scenes.TscnParsing;
using Godot;

namespace Cthangover.Core.Scenes.TscnBuild.SubResourceBuilders
{
	/// <summary>Builds <see cref="StyleBoxFlat"/> from sub_resource properties.</summary>
	public class StyleBoxFlatBuilder : ISubResourceBuilder
	{
		public string ResourceType => "StyleBoxFlat";
		public Resource Build(SubResourceDef def, SubResourceBuildContext ctx) { var b = new StyleBoxFlat(); SubResourceFactory.ApplyProperties(b, def); return b; }
	}
}
