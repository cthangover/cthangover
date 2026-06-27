using Cthangover.Core.UI.Tool;
using Godot;

namespace Cthangover.Tools.InteractiveEditor
{
	public class InteractiveEditorToolProvider : IToolProvider
	{
		public string Id => "interactive_editor";
		public string LocaleKey => "tools/interactive_editor/title";
		public Window CreateWindow() => new InteractiveEditorWindow();
	}

	public class InteractiveEditorToolBoxButton : IToolBoxButton
	{
		public string ToolId => "interactive_editor";
		public string IconPath => "";
		public string LocaleKey => "tools/interactive_editor/title";
		public bool IsVisible() => true;
	}
}
