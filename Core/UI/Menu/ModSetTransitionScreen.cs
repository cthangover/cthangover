namespace Cthangover.Core.UI.Menu
{
    using System;
    using System.Collections.Generic;
    using Cthangover.Core.Localization;
    using Cthangover.Core.Settings;
    using Cthangover.Core.UI.Theming;
    using Godot;

    /// <summary>
    /// Script of <c>res://scenes/menu/mod_set_transition.tscn</c>, the scene the
    /// window shows while the process replaces its mod set (switching games,
    /// hot reload). It must be a kernel scene loaded natively, like the
    /// launcher: the previous scene belongs to mods that are about to be
    /// released, and the next one belongs to mods that are not loaded yet — no
    /// mod scene can exist across that gap. The scene's <see cref="BootLogPanel"/>
    /// streams the compile progress; on a rejected reload
    /// <see cref="ShowErrors"/> swaps it for the compile errors and a way back.
    /// The texts come from <see cref="BootMessages"/> because mod locale is not
    /// available in the gap either.
    /// </summary>
    public partial class ModSetTransitionScreen : Control
    {
        /// <summary>The prefab with the layout of the screen.</summary>
        public const string ScenePath = "res://scenes/menu/mod_set_transition.tscn";

        /// <summary>Makes a new transition screen the current scene (the engine frees the previous scene at the end of the frame).</summary>
        public static ModSetTransitionScreen Show(SceneTree tree)
        {
            var screen = GD.Load<PackedScene>(ScenePath).Instantiate<ModSetTransitionScreen>();
            tree.ChangeSceneToNode(screen);
            return screen;
        }

        /// <summary>Applies the boot theme and the localized title.</summary>
        public override void _Ready()
        {
            CthangoverTheme.Apply(this);
            GetNode<Label>("Title").Text = BootMessages.LoadingMods(Language);
            var back = GetNode<Button>("Errors/Back");
            back.Text = BootMessages.Back(Language);
            CthangoverTheme.ApplyPrimary(back);
        }

        /// <summary>Replaces the progress log with <paramref name="errors"/>; the Back button runs <paramref name="onBack"/>.</summary>
        public void ShowErrors(IEnumerable<string> errors, Action onBack)
        {
            GetNode<Label>("Title").Text = BootMessages.LoadingModsFailed(Language);
            GetNode<Control>("Log").Visible = false;
            GetNode<RichTextLabel>("Errors/ErrorText").Text = string.Join("\n", errors);
            GetNode<Button>("Errors/Back").Pressed += onBack;
            GetNode<Control>("Errors").Visible = true;
        }

        private static string Language => GameData.Instance?.Settings?.Language;
    }
}
