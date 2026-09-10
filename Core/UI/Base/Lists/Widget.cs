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

        /// <summary>Toggles visibility: calls <see cref="Hide"/> if visible, <see cref="Show"/> otherwise.</summary>
        public void Switch()
        {
            if (Visible)
                Hide();
            else
                Show();
        }

        /// <summary>
        /// Makes visible and runs lifecycle. On first call, <see cref="EnsureConstructed"/> fires <see cref="OnceConstruct"/>.
        /// Subsequent calls trigger <see cref="ShowConstruct"/> without reconstruction. Sets both the internal isVisible flag
        /// and Godot's native visibility, then explicitly shows the Body canvas item.
        /// </summary>
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

        /// <summary>Hides the widget, fires <see cref="HideDestruct"/> if constructed, sets internal and Godot visibility to false, and hides the Body canvas item.</summary>
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

        /// <summary>Idempotent construction gate: calls <see cref="OnceConstruct"/> exactly once, on the first invocation.</summary>
        public void EnsureConstructed()
        {
            if (!isConstructed)
            {
                isConstructed = true;
                OnceConstruct();
            }
        }

        /// <summary>Called exactly once on the first <see cref="Show"/>. Override to perform one-time initialization.</summary>
        protected virtual void OnceConstruct()
        {
        }

        /// <summary>Called once on tree exit if constructed. Override for one-time cleanup of resources allocated in <see cref="OnceConstruct"/>.</summary>
        protected virtual void OnceDestruct()
        {
        }

        [Export] private bool isVisible = true;

        [Export] private Node body;

        /// <summary>
        /// Shadows Godot's built-in visibility. Setting to true calls <see cref="Show"/>, setting to false calls <see cref="Hide"/>.
        /// Maintains an independent <c>isVisible</c> flag because Godot's native visibility propagates through the parent chain,
        /// which would break the explicit lifecycle state machine.
        /// </summary>
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

        /// <summary>The rendered content subtree. May be a child node separate from the Rect layout root.</summary>
        public Node Body => body;

        /// <summary>The Control used for layout. For Widget this is the node itself, enabling it to serve as both layout root and widget.</summary>
        public Control Rect => this;

        /// <summary>Called each time <see cref="Show"/> runs after initial construction. Override to refresh state (e.g. reload data, re-enable input).</summary>
        protected virtual void ShowConstruct()
        {
        }

        /// <summary>Called each time <see cref="Hide"/> runs when constructed. Override to clean up display-only state without destroying permanent resources.</summary>
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
