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
        /// <summary>
        /// Index of the active touch (always 0 for the mouse-backed single-touch
        /// model). Exposed for API compatibility with potential multi-touch support.
        /// </summary>
        public static int CurrentTouchIndex { get; set; }

        /// <summary>
        /// Derives a <see cref="TouchPhase"/> from mouse button state across frames.
        /// Compares the current left-button state against <c>wasMouseDownLastFrame</c>:
        /// Began on fresh press, Moved on hold, Ended on release.
        /// </summary>
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

        /// <summary>
        /// Must be called each frame to snapshot the mouse button state. The saved
        /// state drives <see cref="GetTouchPhase"/> for press/release transitions.
        /// </summary>
        public static void Update()
        {
            wasMouseDownLastFrame = Input.IsMouseButtonPressed(MouseButton.Left);
        }

        /// <summary>
        /// Returns 1 when the left mouse button is pressed, 0 otherwise.
        /// Maps the mouse to a single-touch model.
        /// </summary>
        public static int TouchCount
        {
            get
            {
                return Input.IsMouseButtonPressed(MouseButton.Left) ? 1 : 0;
            }
        }

        /// <summary>
        /// Mouse position in UI space (top-left origin, Control coordinate system).
        /// Used for hit-testing against Control nodes in the UI layer.
        /// </summary>
        public static Vector2 UITouchPosition
        {
            get
            {
                var mp = GetViewport()?.GetMousePosition() ?? Vector2.Zero;
                // In Godot, UI coordinates match mouse coordinates (top-left origin in Control nodes)
                return mp;
            }
        }

        /// <summary>
        /// Mouse position in screen space. Used for raycasting against 2D/3D
        /// world objects.
        /// </summary>
        public static Vector2 TouchPosition
        {
            get
            {
                return GetViewport()?.GetMousePosition() ?? Vector2.Zero;
            }
        }

        /// <summary>
        /// Converts <see cref="TouchPosition"/> to a 3D vector (z=0) for ray-origin
        /// calculations in the 3D input pipeline.
        /// </summary>
        public static Vector3 TouchPosition3D
        {
            get
            {
                var pos = TouchPosition;
                return new Vector3(pos.X, pos.Y, 0);
            }
        }

        /// <summary>
        /// Last-frame mouse velocity from Godot's <c>Input.GetLastMouseVelocity()</c>.
        /// Used by drag handlers to compute movement deltas.
        /// </summary>
        public static Vector2 TouchDelta
        {
            get
            {
                return Input.GetLastMouseVelocity();
            }
        }

        /// <summary>
        /// Returns the current touch position. The <paramref name="index"/> parameter
        /// is ignored in the single-touch model — always returns <see cref="TouchPosition"/>.
        /// Exists for multi-touch API compatibility.
        /// </summary>
        /// <param name="index">Touch index (ignored in current implementation).</param>
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
