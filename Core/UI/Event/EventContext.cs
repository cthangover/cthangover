namespace Cthangover.Core.UI.Event
{
    /// <summary>
    /// Mutable context propagated through the input event chain. StopEvent aborts
    /// all further processing; StopSpreading prevents siblings from receiving the
    /// event. Selected and UIClicked carry the target node references for downstream
    /// handlers. Used as a "bag of flags" to coordinate between input pipeline stages.
    /// </summary>
    public class EventContext
    {
        private bool stopSpreading   = false;
        private bool stopEvent       = false;
        private Godot.Node selected  = null;
        private Godot.Node uiClicked = null;

        /// <summary>
        /// When <c>true</c>, prevents the event from propagating to sibling handlers
        /// at the same priority level. The current handler still executes, but
        /// subsequent subscribers in the same list are skipped.
        /// </summary>
        public bool StopSpreading
        {
            get => stopSpreading;
            set { stopSpreading = value; }
        }
        /// <summary>
        /// When <c>true</c>, aborts all further event processing entirely. Neither
        /// sibling handlers nor later-stage handlers (e.g. ray events after UI
        /// click events) will receive the event.
        /// </summary>
        public bool StopEvent {
            get => stopEvent;
            set { stopEvent = value; }
        }
        /// <summary>
        /// The currently selected/hovered node. Set during hit testing so
        /// downstream handlers can query which object is under the cursor.
        /// </summary>
        public Godot.Node Selected {
            get => selected;
            set { selected = value; }
        }
        /// <summary>
        /// The UI node that was clicked. Set when a UI-layer click is detected,
        /// allowing <see cref="IOnUIClickEvent"/> handlers to identify the specific
        /// control element that received the click.
        /// </summary>
        public Godot.Node UIClicked {
            get => uiClicked;
            set { uiClicked = value; }
        }
    }

}
