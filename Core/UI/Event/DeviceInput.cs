using Godot;

namespace Cthangover.Core.UI.Event
{
    /// <summary>
    /// Stateless input abstraction that maps mouse to a virtual single-touch source.
    /// Tracks mouse-down state between frames to derive touch phases (Began/Moved/
    /// Ended/Stationary), enabling touch-aware code to work with mouse-only devices.
    /// Provides both UI-space (UITouchPosition) and screen-space (TouchPosition)
    /// coordinate access. Update() must be called each frame to refresh the
    /// wasMouseDownLastFrame flag.
    /// </summary>
    public static class DeviceInput
    {
        public static int CurrentTouchIndex { get; set; }

        public static TouchPhase GetTouchPhase
        {
            get
            {
                if (Input.IsMouseButtonPressed(MouseButton.Left))
                {
                    if (Input.IsMouseButtonPressed(MouseButton.Left) && !wasMouseDownLastFrame)
                        return TouchPhase.Began;
                    if (Input.IsMouseButtonPressed(MouseButton.Left) && wasMouseDownLastFrame)
                        return TouchPhase.Moved;
                }
                else
                {
                    if (wasMouseDownLastFrame)
                        return TouchPhase.Ended;
                }
                return TouchPhase.Stationary;
            }
        }

        private static bool wasMouseDownLastFrame;

        public static void Update()
        {
            wasMouseDownLastFrame = Input.IsMouseButtonPressed(MouseButton.Left);
        }

        public static int TouchCount
        {
            get
            {
                return Input.IsMouseButtonPressed(MouseButton.Left) ? 1 : 0;
            }
        }

        public static Vector2 UITouchPosition
        {
            get
            {
                var mp = GetViewport()?.GetMousePosition() ?? Vector2.Zero;
                // In Godot, UI coordinates match mouse coordinates (top-left origin in Control nodes)
                return mp;
            }
        }

        public static Vector2 TouchPosition
        {
            get
            {
                return GetViewport()?.GetMousePosition() ?? Vector2.Zero;
            }
        }

        public static Vector3 TouchPosition3D
        {
            get
            {
                var pos = TouchPosition;
                return new Vector3(pos.X, pos.Y, 0);
            }
        }

        public static Vector2 TouchDelta
        {
            get
            {
                return Input.GetLastMouseVelocity();
            }
        }

        public static Vector2 GetTouchPosition(int index)
        {
            return TouchPosition;
        }

        private static Viewport GetViewport()
        {
            var tree = Godot.Engine.GetMainLoop() as SceneTree;
            return tree?.Root?.GetViewport();
        }
    }
}
