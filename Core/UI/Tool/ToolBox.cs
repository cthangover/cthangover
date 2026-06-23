using System.Collections.Generic;
using Cthangover.Core.Scenes;
using Cthangover.Core.UI.Inventory;
using Cthangover.Core.UI.Menu;
using Cthangover.Core.UI.Tool.LightEditor;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.UI.Tool
{

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
            };

            _saveIcon = GetNodeOrNull<TextureRect>("CenterTools/Save");
            UpdateSaveIconVisibility();

            var buttons = GetNodeOrNull<HBoxContainer>("Buttons");
            if (buttons != null)
            {
                ConnectButton(buttons, "MapButton", OnMapClick);
                ConnectButton(buttons, "BagButton", OnBagClick);
                ConnectButton(buttons, "SkillsButton", OnSkillsClick);
                ConnectButton(buttons, "SettingsButton", OnSettingsClick);
                AddLightEditorButton(buttons);
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

        public void OnBagClick()
        {
            GameLogger.Log("TOOLBOX", "bag button clicked");
            Switch("BagWidget");
        }

        public void OnSkillsClick()
        {
            GameLogger.Log("TOOLBOX", "skills button clicked");
            Switch("SkillWidget");
        }

        public void OnCardsClick()
        {
            GameLogger.Log("TOOLBOX", "cards button clicked");
            Switch("CardsWidget");
        }

        public void OnMapClick()
        {
            GameLogger.Log("TOOLBOX", "map button clicked");
            Switch("MapWidget");
        }

        private void AddLightEditorButton(HBoxContainer container)
        {
            var btn = new TextureButton
            {
                CustomMinimumSize = new Vector2(60, 60),
                SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
                IgnoreTextureSize = true,
                StretchMode = TextureButton.StretchModeEnum.KeepAspectCentered
            };
            btn.Pressed += OnLightEditorClick;
            container.AddChild(btn);
        }

        private void OnLightEditorClick()
        {
            LightEditorWindow.Open();
        }

        public void UpdateSaveIconVisibility()
        {
            if (_saveIcon == null)
                return;
            _saveIcon.Visible = SceneManager.IsSaveAllowedForCurrentScene();
        }

    }

}
