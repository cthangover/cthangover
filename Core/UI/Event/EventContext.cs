namespace Cthangover.Core.UI.Event
{

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
