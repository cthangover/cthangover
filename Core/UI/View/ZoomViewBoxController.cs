using Godot;

namespace Cthangover.Core.UI.View
{
    /// <summary>
    /// Thin adapter that exposes ViewBox.SetZoom to external callers (e.g. UI
    /// sliders or scroll handlers) through a single method. Exists as a Node
    /// so it can be placed in the scene tree and receive signals.
    /// </summary>
    public partial class ZoomViewBoxController : Node
    {

        [Export] private ViewBox viewBox;

        /// <summary>
        /// Delegates to <see cref="ViewBox.SetZoom"/> on the linked <see cref="ViewBox"/>.
        /// Intended to be connected to a slider or scroll input signal. No-op if
        /// <c>viewBox</c> is not assigned.
        /// </summary>
        /// <param name="value">Normalized zoom factor (0–1).</param>
        public void OnChangeZoom(float value)
        {
            if (viewBox == null)
                return;

            viewBox.SetZoom(value);
        }

    }

}
