using Cthangover.Core.Factories.Impls;
using Cthangover.Core.Mods;
using Cthangover.Core.Scenes;
using Cthangover.Core.UI.Theming;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.UI.Menu
{
    /// <summary>
    /// Materializes the <c>main_menu</c> buttons collected by
    /// <see cref="MainMenuRegistry"/> into the <c>ModMenuButtons</c> container of
    /// the main menu scene. Buttons are created once per menu instantiation,
    /// sorted by the registry, and open their mod scene as a full-rect child of
    /// the menu root — the scene then owns its lifetime and closes itself.
    /// <para/>
    /// The button carries its translation key in node metadata
    /// (<c>main_menu_locale</c>) so <see cref="MainMenu"/> can retranslate it on
    /// language change together with the built-in buttons.
    /// </summary>
    public static class MainMenuBuilder
    {
        /// <summary>Node metadata key holding the button's translation key.</summary>
        public const string LocaleMetaKey = "main_menu_locale";

        /// <summary>
        /// Creates one button per registry entry inside <paramref name="container"/>.
        /// The host menu is passed in because pressed handlers instantiate mod
        /// scenes as its children.
        /// </summary>
        public static void Build(Control menuRoot, Container container)
        {
            if (menuRoot == null || container == null || !GodotObject.IsInstanceValid(container))
                return;

            foreach (var entry in MainMenuRegistry.Entries)
                container.AddChild(CreateButton(menuRoot, entry));
        }

        private static Button CreateButton(Control menuRoot, MainMenuEntry entry)
        {
            var title = TranslationServer.Translate(entry.Button.Locale);
            var button = new Button
            {
                Text = title,
                TooltipText = title,
            };

            if (!string.IsNullOrEmpty(entry.Button.Icon))
            {
                var icon = UIIconFactory.Instance.Get(entry.Button.Icon);
                if (icon != null)
                    button.Icon = icon;
            }

            button.SetMeta(LocaleMetaKey, entry.Button.Locale);
            button.Pressed += () => OpenScene(menuRoot, entry);
            return button;
        }

        private static void OpenScene(Control menuRoot, MainMenuEntry entry)
        {
            var node = new ModScenePrefab(entry.ScenePath).Instantiate();
            if (node == null)
            {
                GameLogger.Log("MAIN_MENU_BUILDER", $"Open '{entry.ModId}/{entry.Id}': scene '{entry.ScenePath}' failed to build", LogLevel.Error);
                return;
            }

            menuRoot.AddChild(node);
            CthangoverTheme.Apply(node);

            if (node is Control control)
                control.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

            if (node is ModWidget modWidget)
                modWidget.EnsureConstructed();

            GameLogger.Log("MAIN_MENU_BUILDER", $"Opened '{entry.ModId}/{entry.Id}' → {entry.ScenePath}");
        }
    }
}
