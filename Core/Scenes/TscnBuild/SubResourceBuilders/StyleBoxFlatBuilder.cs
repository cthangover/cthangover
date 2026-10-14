using Cthangover.Core.Scenes.TscnParsing;
using Godot;

namespace Cthangover.Core.Scenes.TscnBuild.SubResourceBuilders
{
	/// <summary>Builds <see cref="StyleBoxFlat"/> from sub_resource properties.</summary>
	public class StyleBoxFlatBuilder : ISubResourceBuilder
	{
		/// <summary>Handles <c>[sub_resource type="StyleBoxFlat"]</c> — the most common themed panel box.</summary>
		public string ResourceType => "StyleBoxFlat";
		/// <summary>Creates a <see cref="StyleBoxFlat"/> and applies bg_color, corner radii, and other parsed properties.</summary>
		public Resource Build(SubResourceDef def, SubResourceBuildContext ctx) { var b = new StyleBoxFlat(); SubResourceFactory.ApplyProperties(b, def); return b; }
	}
}
