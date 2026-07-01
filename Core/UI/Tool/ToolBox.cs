using System.Collections.Generic;
using Cthangover.Core.Scenes;
using Cthangover.Core.UI.CharacterPanel;
using Cthangover.Core.UI.Inventory;
using Cthangover.Core.UI.Menu;
using Cthangover.Core.UI.SkillsPanel;
using Cthangover.Core.UI.Tool;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.UI.Tool
{
    /// <summary>
    /// Main toolbar/HUD container. Manages a set of named Widget instances
    /// (map, inventory bag, skills, cards) and toggles between them via Switch(),
    /// auto-hiding all others. Buttons are wired by name from an "Buttons"
    /// HBoxContainer child, supporting both regular Buttons and TextureButtons.
    /// AddToolButtons dynamically creates buttons for mod-registered tools from
    /// ToolBoxButtonFactory. Updates the save icon visibility based on
    /// SceneManager.IsSaveAllowedForCurrentScene. FindWidget uses recursive
    /// tree search so widgets can be placed anywhere in the scene hierarchy.
    /// </summary>
    public partial class ToolBox : Control
    {
        private Dictionary<string, Widget> tools;
        private TextureRect _saveIcon;

        public override void _Ready()
        {
            tools = new Dictionary<string, Widget>
            {
                {"MapWidget",   FindWidget<MapWidget>()},
                {"BagWidget",   FindWidget<PlayerInventoryBagBehaviour>()},
                {"CharactersWidget", FindWidget<CharacterPanelBehaviour>()},
                {"SkillWidget", FindWidget<SkillsPanelBehaviour>()},
            };

            _saveIcon = GetNodeOrNull<TextureRect>("CenterTools/Save");
            UpdateSaveIconVisibility();

            var buttons = GetNodeOrNull<HBoxContainer>("Buttons");
            if (buttons != null)
            {
                ConnectButton(buttons, "MapButton", OnMapClick);
                ConnectButton(buttons, "BagButton", OnBagClick);
                ConnectButton(buttons, "CharactersButton", OnCharactersClick);
                ConnectButton(buttons, "SkillsButton", OnSkillsClick);
                ConnectButton(buttons, "SettingsButton", OnSettingsClick);
                AddToolButtons(buttons);
            }
        }

        private void ConnectButton(HBoxContainer container, string name, System.Action callback)
        {
            var btn = container.GetNodeOrNull<Button>(name);
            if (btn != null)
            {
                btn.Pressed += callback;
                return;
            }
            var texBtn = container.GetNodeOrNull<TextureButton>(name);
            if (texBtn != null)
                texBtn.Pressed += callback;
        }

        private T FindWidget<T>() where T : Widget
        {
            var tree = GetTree();
            if (tree?.Root == null)
            {
                GameLogger.Log("TOOLBOX", $"FindWidget<{typeof(T).Name}>: tree root is null");
                return null;
            }
            return FindWidgetRecursive<T>(tree.Root);
        }

        private T FindWidgetRecursive<T>(Node node) where T : Widget
        {
            if (node is T widget)
            {
                GameLogger.Log("TOOLBOX", $"FindWidget<{typeof(T).Name}>: found '{widget.Name}' in tree");
                return widget;
            }
            var childCount = node.GetChildCount();
            for (int i = 0; i < childCount; i++)
            {
                var result = FindWidgetRecursive<T>(node.GetChild(i));
                if (result != null)
                    return result;
            }
            return null;
        }

        private void Switch(string widgetName)
        {
            if (!tools.TryGetValue(widgetName, out var widget) || widget == null)
            {
                GameLogger.Log("TOOLBOX", $"switch '{widgetName}' failed: widget not found");
                return;
            }

            GameLogger.Log("TOOLBOX", $"switch to '{widgetName}'");

            foreach (var tool in tools.Values)
            {
                if (tool == null)
                    continue;
                if (tool == widget)
                    tool.Switch();
                else
                    tool.Hide();
            }
        }

        /// <summary>
        /// Opens the in-game <see cref="GameMenu"/>. Refreshes the save button state first
        /// so it correctly reflects whether the current scene allows saving.
        /// </summary>
        public void OnSettingsClick()
        {
            GameLogger.Log("TOOLBOX", "settings button clicked");
            var gameMenu = SceneContextNode.FindNode<GameMenu>("GameMenu");
            if (gameMenu != null)
            {
                gameMenu.RefreshSaveButton();
                gameMenu.Visible = true;
            }
        }

        /// <summary>Switches to the inventory bag widget, hiding all other toolbox widgets.</summary>
        public void OnBagClick()
        {
            GameLogger.Log("TOOLBOX", "bag button clicked");
            Switch("BagWidget");
        }

        /// <summary>Switches to the skills widget, hiding all other toolbox widgets.</summary>
        public void OnSkillsClick()
        {
            GameLogger.Log("TOOLBOX", "skills button clicked");
            Switch("SkillWidget");
        }

        /// <summary>Switches to the cards widget, hiding all other toolbox widgets.</summary>
        public void OnCardsClick()
        {
            GameLogger.Log("TOOLBOX", "cards button clicked");
            Switch("CardsWidget");
        }

        /// <summary>Switches to the map widget, hiding all other toolbox widgets.</summary>
        public void OnMapClick()
        {
            GameLogger.Log("TOOLBOX", "map button clicked");
            Switch("MapWidget");
        }

        /// <summary>Switches to the character panel widget, hiding all other toolbox widgets.</summary>
        public void OnCharactersClick()
        {
            GameLogger.Log("TOOLBOX", "characters button clicked");
            Switch("CharactersWidget");
        }

        private void AddToolButtons(HBoxContainer container)
        {
            foreach (var buttonDef in ToolBoxButtonFactory.Instance.GetVisible())
            {
                var btn = new TextureButton
                {
                    CustomMinimumSize = new Vector2(60, 60),
                    SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
                    IgnoreTextureSize = true,
                    StretchMode = TextureButton.StretchModeEnum.KeepAspectCentered
                };
                var toolId = buttonDef.ToolId;
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
