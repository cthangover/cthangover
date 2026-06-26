using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.UI
{
    /// <summary>
    /// Base widget with a lazy construct/destruct lifecycle around Show/Hide.
    /// On first Show(), OnceConstruct() fires once via EnsureConstructed — subsequent
    /// Show/Hide calls trigger ShowConstruct/HideDestruct instead, allowing repeated
    /// state reset without full reconstruction. Overrides Godot's built-in Visible
    /// property to intercept visibility changes and route them through the lifecycle.
    /// Maintains an independent isVisible flag because Godot's visibility has quirks
    /// (e.g. parent visibility propagation) that would break the state machine.
    /// The #if TOOLS _ValidateProperty syncs the editor inspector with the override.
    /// </summary>
    public abstract partial class Widget : Control, IWidget
    {
        private bool isConstructed;

        public override void _Ready()
        {
            GameLogger.Log("WIDGET", $"_Ready: name={Name}, isVisible={isVisible}, GodotVisible={base.Visible}", LogLevel.Debug);
            isVisible = base.Visible;
            if (!isVisible)
                Hide();
        }

        public override void _ExitTree()
        {
            if (isConstructed)
                OnceDestruct();
        }

        public void Switch()
        {
            if (Visible)
                Hide();
            else
                Show();
        }

        public new virtual void Show()
        {
            GameLogger.Log("WIDGET", $"Show ENTER: name={Name}, isVisible={isVisible}, GodotVisible={base.Visible}", LogLevel.Debug);
            var wasConstructed = isConstructed;
            EnsureConstructed();

            if (Visible)
            {
                if (!wasConstructed)
                    ShowConstruct();
                GameLogger.Log("WIDGET", $"Show SKIP: already visible", LogLevel.Debug);
                return;
            }

            ShowConstruct();

            isVisible = true;
            base.Visible = true;

            var canvasBody = Body as CanvasItem;
            if (canvasBody != null)
                canvasBody.Visible = true;

            (Body as Control)?.QueueRedraw();
            
            GameLogger.Log("WIDGET", $"Show EXIT: name={Name}, isVisible={isVisible}, GodotVisible={base.Visible}", LogLevel.Debug);
        }

        public new virtual void Hide()
        {
            if (!Visible)
                return;

            if (isConstructed)
                HideDestruct();

            isVisible = false;
            base.Visible = false;

            var canvasBody = Body as CanvasItem;
            if (canvasBody != null)
                canvasBody.Visible = false;
        }

        public void EnsureConstructed()
        {
            if (!isConstructed)
            {
                isConstructed = true;
                OnceConstruct();
            }
        }

        protected virtual void OnceConstruct()
        {
        }

        protected virtual void OnceDestruct()
        {
        }

        [Export] private bool isVisible = true;

        [Export] private Node body;

        public new bool Visible
        {
            get => isVisible;
            set
            {
                if (isVisible == value)
                    return;

                if (value)
                    Show();
                else
                    Hide();
            }
        }

        public Node Body => body;

        public Control Rect => this;

        protected virtual void ShowConstruct()
        {
        }

        protected virtual void HideDestruct()
        {
        }

#if TOOLS
		public override void _ValidateProperty(Godot.Collections.Dictionary property)
		{
			base._ValidateProperty(property);
			if (property.TryGetValue("visible", out var _))
			{
				isVisible = base.Visible;
				if (isVisible)
					Show();
				else
					Hide();
			}
			else if (property.TryGetValue("isVisible", out var _))
			{
				if (isVisible)
					Show();
				else
					Hide();
			}
		}
#endif
    }
}
