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
        /// <summary>
        /// Creates a new instance of <typeparamref name="T"/> and displays it as a centered popup
        /// on the scene tree root. Returns the window so the caller can inspect it after closing.
        /// </summary>
        /// <typeparam name="T">A concrete <see cref="ToolWindow"/> subclass with a parameterless constructor.</typeparam>
        public static T Open<T>() where T : ToolWindow, new()
        {
            var window = new T();
            var tree = Godot.Engine.GetMainLoop() as SceneTree;
            tree?.Root.AddChild(window);
            window.PopupCentered(DisplayServer.WindowGetSize());
            return window;
        }

        /// <summary>
        /// Displays an already-constructed <see cref="Window"/> as a centered popup on the scene
        /// tree root. Used when the window was created via <see cref="IToolProvider.CreateWindow"/>
        /// rather than the generic <see cref="Open{T}"/> factory.
        /// </summary>
        public static Window ShowWindow(Window window)
        {
            var tree = Godot.Engine.GetMainLoop() as SceneTree;
            tree?.Root.AddChild(window);
            window.PopupCentered(DisplayServer.WindowGetSize());
            return window;
        }

        /// <summary>
        /// Whether the window has unsaved changes. When <c>true</c>, the title bar shows a "*" prefix
        /// and <see cref="HandleCloseRequested"/> prompts for confirmation before closing.
        /// </summary>
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

        /// <summary>
        /// Override to release resources when the window is closed. Called from <c>_ExitTree</c>.
        /// </summary>
        protected virtual void Cleanup() { }

        /// <summary>
        /// Marks the window as having unsaved changes. Prepends "* " to the title bar text
        /// as a visual indicator. Idempotent — calling multiple times has no additional effect.
        /// </summary>
        protected void SetDirty()
        {
            if (!_dirty)
            {
                _dirty = true;
                Title = "* " + _baseTitle;
            }
        }

        /// <summary>
        /// Clears the dirty flag and restores the original title bar text.
        /// </summary>
        protected void MarkClean()
        {
            _dirty = false;
            Title = _baseTitle;
        }

        /// <summary>
        /// Shows a localized "unsaved changes" confirmation dialog. If the user confirms,
        /// executes <paramref name="onDiscard"/> (typically <c>QueueFree</c>) before freeing the dialog.
        /// </summary>
        /// <param name="onDiscard">Callback invoked when the user clicks OK.</param>
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

        /// <summary>
        /// Shows the unsaved-changes confirmation and returns <c>true</c> if the user confirms
        /// discard or if the window is clean. Note: the dialog is modal but this method returns
        /// immediately — the result is captured via a signal callback and may not be meaningful
        /// for callers expecting synchronous flow. See <see cref="ShowUnsavedDialog"/> for an
        /// alternative callback-based approach.
        /// </summary>
        /// <returns><c>true</c> if the window was clean when called.</returns>
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

        /// <summary>
        /// Creates a full-anchor <see cref="VBoxContainer"/> that fills the entire content area
        /// of the window, with both horizontal and vertical expand flags.
        /// </summary>
        protected static VBoxContainer CreateFillContainer()
        {
            var c = new VBoxContainer();
            c.AnchorRight = 1f;
            c.AnchorBottom = 1f;
            c.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            c.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            return c;
        }

        /// <summary>
        /// Creates a pre-configured <see cref="HBoxContainer"/> with 8px spacing, suitable
        /// for tool window top toolbars.
        /// </summary>
        protected static HBoxContainer CreateToolbar()
        {
            var t = new HBoxContainer();
            t.AddThemeConstantOverride("separation", 8);
            return t;
        }

        /// <summary>
        /// Wraps the given <paramref name="content"/> in a fixed-width <see cref="PanelContainer"/>
        /// with vertical scrolling (horizontal scroll disabled). Typical sidebar for tool windows.
        /// </summary>
        /// <param name="content">The VBoxContainer content to display inside the sidebar.</param>
        /// <param name="width">Fixed minimum width in pixels (default 280).</param>
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

        /// <summary>
        /// Resolves a monospace font for code or debug displays. Tries <c>RobotoMono.ttf</c>
        /// from the bundled assets first; falls back to <see cref="ThemeDB.FallbackFont"/> if
        /// the resource is missing or fails to load.
        /// </summary>
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
