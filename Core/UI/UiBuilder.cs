using Cthangover.Core.Bootstrap;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.UI
{
    /// <summary>
    /// Centralized, one-shot construction of the mod GUI for a freshly loaded
    /// shell. Kept deliberately dumb: it never reacts to intermediate states and
    /// never rebuilds — <see cref="UiHost"/> calls it exactly once per
    /// <c>ShellReadyEvent</c>, and <see cref="IToolbarHost.ApplyPanelButtons"/> owns the
    /// actual node creation.
    /// </summary>
    public static class UiBuilder
    {
        /// <summary>
        /// Asks the shell toolbar to materialize the buttons declared by panel-kind
        /// entries. Panel scenes themselves are built lazily on first toggle.
        /// </summary>
        public static void Build(Node shellRoot)
        {
            if (shellRoot == null || !GodotObject.IsInstanceValid(shellRoot))
                return;

            if (shellRoot.FindChild("TopView", true, false) is not IToolbarHost toolBox)
            {
                GameLogger.Log("UI_BUILDER", "Build: ToolBox 'TopView' not found in shell", LogLevel.Warning);
                return;
            }

            toolBox.ApplyPanelButtons(AppServices.Current.Ui.AllPanels);
        }
    }
}
