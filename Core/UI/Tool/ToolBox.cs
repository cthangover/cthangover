using System.Collections.Generic;
using Cthangover.Core.Factories.Impls;
using Cthangover.Core.Mods;
using Cthangover.Core.Scenes;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.UI.Tool
{
    /// <summary>
    /// Main toolbar/HUD container (the <c>TopView</c> shell node). It no longer
    /// knows any concrete panel: buttons for mod-provided panels are handed to it
    /// by <see cref="UiBuilder"/> from <see cref="UiRegistry.PanelEntries"/>, and
    /// pressing one calls <see cref="UiRegistry.Toggle"/>. Tool buttons
    /// (<see cref="IToolBoxButton"/>/<see cref="ToolFactory"/>) remain a separate,
    /// self-registering mechanism and are added alongside the panel buttons.
    /// </summary>
    public partial class ToolBox : Control
    {
        private HBoxContainer _buttons;
        private TextureRect _saveIcon;

        public override void _Ready()
        {
            _buttons = GetNodeOrNull<HBoxContainer>("Buttons");
            _saveIcon = GetNodeOrNull<TextureRect>("CenterTools/Save");
            UpdateSaveIconVisibility();

            AddToolButtons(_buttons);
        }

        /// <summary>
        /// Materializes a toolbar button for every entry. Called once per shell by
        /// <see cref="UiBuilder"/>; entries arrive pre-sorted by the registry
        /// (button index, then mod load order, then id).
        /// </summary>
        public void ApplyPanelButtons(IReadOnlyList<UiEntry> entries)
        {
            if (_buttons == null || entries == null)
                return;

            foreach (var entry in entries)
                _buttons.AddChild(CreatePanelButton(entry));
        }

        private static BaseButton CreatePanelButton(UiEntry entry)
        {
            var title = TranslationServer.Translate(entry.Button.Locale);
            var icon = UIIconFactory.Instance.Get(entry.Button.Icon);

            BaseButton button;
            if (icon != null)
            {
                button = new TextureButton
                {
                    TextureNormal = icon,
                    CustomMinimumSize = new Vector2(60, 60),
                    SizeFlagsVertical = SizeFlags.ShrinkCenter,
                    IgnoreTextureSize = true,
                    StretchMode = TextureButton.StretchModeEnum.KeepAspectCentered
                };
            }
            else
            {
                button = new Button
                {
                    Text = title,
                    CustomMinimumSize = new Vector2(60, 60),
                    SizeFlagsVertical = SizeFlags.ShrinkCenter
                };
            }

            var id = entry.Id;
            button.TooltipText = title;
            button.Pressed += () => UiRegistry.Toggle(id);
            return button;
        }

        private void AddToolButtons(HBoxContainer container)
        {
            if (container == null)
                return;

            foreach (var buttonDef in ToolBoxButtonFactory.Instance.GetVisible())
            {
                var toolId = buttonDef.ToolId;
                var title = TranslationServer.Translate(buttonDef.LocaleKey);

                Texture2D icon = null;
                if (!string.IsNullOrEmpty(buttonDef.IconPath) && ResourceLoader.Exists(buttonDef.IconPath))
                    icon = ResourceLoader.Load<Texture2D>(buttonDef.IconPath);

                BaseButton btn;
                if (icon != null)
                {
                    btn = new TextureButton
                    {
                        TextureNormal = icon,
                        CustomMinimumSize = new Vector2(60, 60),
                        SizeFlagsVertical = SizeFlags.ShrinkCenter,
                        IgnoreTextureSize = true,
                        StretchMode = TextureButton.StretchModeEnum.KeepAspectCentered
                    };
                }
                else
                {
                    btn = new Button
                    {
                        Text = title,
                        CustomMinimumSize = new Vector2(60, 60),
                        SizeFlagsVertical = SizeFlags.ShrinkCenter
                    };
                }

                btn.TooltipText = title;
                btn.Pressed += () =>
                {
                    var tool = ToolFactory.Instance.Get(toolId);
                    if (tool != null)
                        ToolWindow.ShowWindow(tool.CreateWindow());
                };
                container.AddChild(btn);
            }
        }

        /// <summary>
        /// Shows or hides the save icon based on <see cref="SceneManager.IsSaveAllowedForCurrentScene"/>.
        /// Call after scene transitions to reflect the new scene's save policy.
        /// </summary>
        public void UpdateSaveIconVisibility()
        {
            if (_saveIcon == null)
                return;
            _saveIcon.Visible = SceneManager.IsSaveAllowedForCurrentScene();
        }
    }
}
