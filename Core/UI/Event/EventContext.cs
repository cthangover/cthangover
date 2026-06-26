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

        public bool StopSpreading
        {
            get => stopSpreading;
            set { stopSpreading = value; }
        }
        public bool StopEvent {
            get => stopEvent;
            set { stopEvent = value; }
        }
        public Godot.Node Selected {
            get => selected;
            set { selected = value; }
        }
        public Godot.Node UIClicked {
            get => uiClicked;
            set { uiClicked = value; }
        }
    }

}
