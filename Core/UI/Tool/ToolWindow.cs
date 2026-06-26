using System;
using Godot;

namespace Cthangover.Core.UI.Tool
{
    /// <summary>
    /// Base for tool popup windows with dirty-state tracking. When dirty, the
    /// title gains a "*" prefix — a common editor convention. CloseRequested
    /// shows an unsaved-changes confirmation if dirty. ShowUnsavedDialog and
    /// ConfirmDiscardUnsaved provide two patterns: callback-based and return-value-
    /// based, for different use cases. Provides convenience factory methods for
    /// common layout widgets (CreateFillContainer, CreateToolbar, CreateSidebar)
    /// to reduce boilerplate in tool implementations. GetMonospaceFont() falls
    /// back to ThemeDB.FallbackFont if the bundled monospace font is missing.
    /// </summary>
    public abstract partial class ToolWindow : Window
    {
        public static T Open<T>() where T : ToolWindow, new()
        {
            var window = new T();
            var tree = Godot.Engine.GetMainLoop() as SceneTree;
            tree?.Root.AddChild(window);
            window.PopupCentered(DisplayServer.WindowGetSize());
            return window;
        }

        public static Window ShowWindow(Window window)
        {
            var tree = Godot.Engine.GetMainLoop() as SceneTree;
            tree?.Root.AddChild(window);
            window.PopupCentered(DisplayServer.WindowGetSize());
            return window;
        }

        protected bool _dirty;
        private string _baseTitle;

        protected ToolWindow(string titleKey)
        {
            _baseTitle = TranslationServer.Translate(titleKey);
            Title = _baseTitle;
            Unresizable = false;
            CloseRequested += HandleCloseRequested;
        }

        public override void _ExitTree()
        {
            Cleanup();
        }

        protected virtual void Cleanup() { }

        protected void SetDirty()
        {
            if (!_dirty)
            {
                _dirty = true;
                Title = "* " + _baseTitle;
            }
        }

        protected void MarkClean()
        {
            _dirty = false;
            Title = _baseTitle;
        }

        protected void ShowUnsavedDialog(Action onDiscard)
        {
            var dialog = new ConfirmationDialog();
            dialog.Title = TranslationServer.Translate("tools/unsaved_title");
            dialog.DialogText = TranslationServer.Translate("tools/unsaved_text");
            dialog.Confirmed += () => { dialog.QueueFree(); onDiscard(); };
            dialog.Canceled += () => dialog.QueueFree();
            AddChild(dialog);
            dialog.PopupCentered();
        }

        protected bool ConfirmDiscardUnsaved()
        {
            if (!_dirty)
                return true;

            var dialog = new ConfirmationDialog();
            dialog.Title = TranslationServer.Translate("tools/unsaved_title");
            dialog.DialogText = TranslationServer.Translate("tools/unsaved_text");
            AddChild(dialog);

            bool result = false;
            dialog.Confirmed += () => { result = true; dialog.QueueFree(); };
            dialog.Canceled += () => { result = false; dialog.QueueFree(); };
            dialog.PopupCentered();

            return result;
        }

        private void HandleCloseRequested()
        {
            if (_dirty)
                ShowUnsavedDialog(() => QueueFree());
            else
                QueueFree();
        }

        protected static VBoxContainer CreateFillContainer()
        {
            var c = new VBoxContainer();
            c.AnchorRight = 1f;
            c.AnchorBottom = 1f;
            c.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            c.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            return c;
        }

        protected static HBoxContainer CreateToolbar()
        {
            var t = new HBoxContainer();
            t.AddThemeConstantOverride("separation", 8);
            return t;
        }

        protected static PanelContainer CreateSidebar(VBoxContainer content, int width = 280)
        {
            var panel = new PanelContainer();
            panel.CustomMinimumSize = new Vector2(width, 0);
            panel.SizeFlagsVertical = Control.SizeFlags.ExpandFill;

            var scroll = new ScrollContainer();
            scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
            panel.AddChild(scroll);
            scroll.AddChild(content);

            return panel;
        }

        protected static Font GetMonospaceFont()
        {
            try
            {
                if (ResourceLoader.Exists("res://assets/fonts/RobotoMono.ttf"))
                    return ResourceLoader.Load<FontFile>("res://assets/fonts/RobotoMono.ttf");
            }
            catch { }
            return ThemeDB.FallbackFont;
        }
    }
}
