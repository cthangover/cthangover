namespace Cthangover.Mods.ScenarioStudio
{
    using System;
    using Cthangover.Core.UI.Theming;
    using Godot;

    /// <summary>
    /// The dock's small modal questions: a one-line text input (edit a line,
    /// name a scene or an exported mod) and a pick list (where an answer
    /// leads, which scene to link). They are plain Godot popups themed like the
    /// game, added under the dock and freed when closed.
    /// </summary>
    public static class StudioPrompt
    {
        /// <summary>Asks for a line of text; <paramref name="done"/> gets it on OK.</summary>
        public static ConfirmationDialog AskText(Node parent, string title, string initial, Action<string> done)
        {
            var dialog = new ConfirmationDialog { Title = title, MinSize = new Vector2I(460, 0) };
            CthangoverTheme.Apply(dialog);
            dialog.OkButtonText = TranslationServer.Translate("studio/ok");
            dialog.CancelButtonText = TranslationServer.Translate("studio/cancel");

            var field = new LineEdit { Text = initial ?? string.Empty, SelectAllOnFocus = true };
            dialog.AddChild(field);
            dialog.RegisterTextEnter(field);

            var relay = new StudioPromptRelay(field, done);
            dialog.Confirmed += relay.OnConfirmed;
            dialog.Confirmed += dialog.QueueFree;
            dialog.Canceled += dialog.QueueFree;
            dialog.AddChild(relay);

            parent.AddChild(dialog);
            dialog.PopupCentered();
            field.GrabFocus();
            return dialog;
        }

        /// <summary>Shows a pick list under the mouse; <paramref name="picked"/> gets the item id.</summary>
        public static PopupMenu Pick(Node parent, PopupMenu.IdPressedEventHandler picked)
        {
            var menu = new PopupMenu();
            CthangoverTheme.Apply(menu);
            menu.IdPressed += picked;
            menu.PopupHide += menu.QueueFree;
            parent.AddChild(menu);
            return menu;
        }

        /// <summary>Opens a pick list built with <see cref="Pick"/> at the mouse position.</summary>
        public static void Open(PopupMenu menu)
        {
            menu.ResetSize();
            menu.Position = (Vector2I)menu.GetViewport().GetMousePosition();
            menu.Popup();
        }
    }
}
