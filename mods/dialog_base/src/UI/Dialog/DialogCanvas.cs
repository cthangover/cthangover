using Cthangover.Core.Scenes;
using Cthangover.Core.Utils;
using Godot;
using Cthangover.Core.UI;
using Cthangover.Mods.Interface.Base.Lists;
using Cthangover.Core.UI.Dialog;
using Cthangover.Core.UI.Event;
using Cthangover.Core.UI.Executable;
using Cthangover.Core.Scenarios;
using Cthangover.Core.Interactive;

namespace Cthangover.Mods.Dialog
{
    /// <summary>
    /// Thin wrapper node that hosts the dialog UI. The canvas instantiates its
    /// <see cref="DialogBox"/> child at runtime from the core mod through
    /// <see cref="ModScenePrefab"/> and keeps the well-known node name
    /// <c>DialogBox</c>. The rest of the game locates it by that name via
    /// <see cref="SceneContextNode.FindNode{T}"/>, not by hardcoded paths.
    /// </summary>
    public partial class DialogCanvas : Control
    {
        private static readonly ModScenePrefab DialogBoxPrefab = new("scenes/ui/dialog_box.tscn");

        private DialogBox dialogBox;

        /// <summary>The active DialogBox child, resolved in _Ready. Used by the scene tree root to access dialog without path traversal.</summary>
        public DialogBox DialogBox => dialogBox;

        /// <summary>Instantiates the <see cref="DialogBox"/> child from the core prefab when the scene does not ship one, keeping the well-known node name for path-free lookup.</summary>
        public override void _Ready()
        {
            dialogBox ??= GetNodeOrNull<DialogBox>("DialogBox");
            if (dialogBox == null)
            {
                dialogBox = DialogBoxPrefab.Instantiate<DialogBox>();
                if (dialogBox != null)
                {
                    dialogBox.Name = "DialogBox";
                    AddChild(dialogBox);
                }
                else
                {
                    GameLogger.Log("DIALOG", "DialogCanvas._Ready: failed to build 'scenes/ui/dialog_box.tscn'", LogLevel.Error);
                }
            }

            MouseFilter = MouseFilterEnum.Ignore;
        }
    }

}
