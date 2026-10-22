using Cthangover.Core.Scenes;
using Cthangover.Core.UI;
using Cthangover.Mods.Dialog;
using Godot;

namespace Cthangover.Mods.Rpg
{
    /// <summary>
    /// Keyboard of the RPG world. Keys are read by their <b>physical</b> position
    /// (<see cref="Input.IsPhysicalKeyPressed"/>), so WASD and E/F stay where
    /// they are on a Russian or any other layout; the arrows work too.
    /// <para/>
    /// The world must not walk while something else owns the keyboard or the
    /// player's attention: a dialog, a toolbar panel (inventory, journal) or a
    /// window-like overlay opened by a scenario (a trade window, a workbench), a
    /// focused editor window or text field, a paused tree, a scene transition.
    /// <see cref="IsWalkBlocked"/> collects those checks in one place.
    /// </summary>
    public static class RpgInput
    {
        /// <summary>The held direction, or null. Vertical keys win over horizontal ones when both are held.</summary>
        public static RpgDirection? HeldDirection()
        {
            if (Input.IsPhysicalKeyPressed(Key.W) || Input.IsPhysicalKeyPressed(Key.Up))
                return RpgDirection.Up;
            if (Input.IsPhysicalKeyPressed(Key.S) || Input.IsPhysicalKeyPressed(Key.Down))
                return RpgDirection.Down;
            if (Input.IsPhysicalKeyPressed(Key.A) || Input.IsPhysicalKeyPressed(Key.Left))
                return RpgDirection.Left;
            if (Input.IsPhysicalKeyPressed(Key.D) || Input.IsPhysicalKeyPressed(Key.Right))
                return RpgDirection.Right;
            return null;
        }

        /// <summary>True for a fresh press of the action key (E or F).</summary>
        public static bool IsActionPress(InputEvent @event)
        {
            return @event is InputEventKey key && key.Pressed && !key.Echo
                && (key.PhysicalKeycode == Key.E || key.PhysicalKeycode == Key.F);
        }

        /// <summary>The shell's dialog box, or null outside the shell.</summary>
        public static DialogBox FindDialogBox() => SceneContextNode.FindNode<DialogBox>("DialogBox");

        /// <summary>True when the player must not walk or interact right now.</summary>
        public static bool IsWalkBlocked(Node node)
        {
            if (node == null || !node.IsInsideTree() || node.GetTree().Paused || GodotSceneService.IsTransitioning)
                return true;
            if (FindDialogBox()?.IsRunning == true)
                return true;

            var viewport = node.GetViewport();
            var focus = viewport.GuiGetFocusOwner();
            if (focus is LineEdit || focus is TextEdit)
                return true;
            foreach (var window in viewport.GetEmbeddedSubwindows())
            {
                if (window.Visible && window.HasFocus())
                    return true;
            }
            return IsPanelOpen();
        }

        // Panels and "once" overlays opened on demand are window-like entries on the
        // host layer. Overlays shown from the start (a HUD strip) do not block walking.
        private static bool IsPanelOpen()
        {
            var host = UiHost.Instance;
            if (host == null)
                return false;
            foreach (var entry in UiRegistry.Entries)
            {
                if (!UiRegistry.IsPanel(entry) && !(entry.Once && !entry.ShowOnStart))
                    continue;
                if (host.TryGetInstance(entry.Id, out var panel) && panel.IsVisibleInTree())
                    return true;
            }
            return false;
        }
    }
}
