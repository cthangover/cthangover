namespace Cthangover.Core.Settings
{
    using System.Collections.Generic;
    using Godot;

    /// <summary>
    /// The choices the display settings offer and how a saved value maps onto
    /// them — shared by the first-run <c>Launcher</c> and the settings menu of
    /// <c>menu_base</c>, so both show the same lists and read the same file.
    /// Pure data (no engine calls); <see cref="DisplaySettingsService"/> puts the
    /// chosen values on the window.
    /// <para/>
    /// Window size ("resolution") matters only in windowed mode: the fullscreen
    /// modes run at the monitor's own resolution, and the canvas_items stretch
    /// scales the base viewport into whatever the window is. The size list is
    /// therefore built per monitor and never offers a window larger than the
    /// monitor; a size the player saved earlier stays selectable even when it is
    /// not a common one (a window resized on another machine).
    /// </summary>
    public static class DisplayOptions
    {
        /// <summary>Window modes in menu order; minimized is never offered.</summary>
        public static readonly DisplayServer.WindowMode[] WindowModes =
        {
            DisplayServer.WindowMode.Windowed,
            DisplayServer.WindowMode.Maximized,
            DisplayServer.WindowMode.Fullscreen,
            DisplayServer.WindowMode.ExclusiveFullscreen,
        };

        /// <summary>Locale keys of <see cref="WindowModes"/>, same order.</summary>
        public static readonly string[] WindowModeKeys =
        {
            "settings/window_mode_windowed",
            "settings/window_mode_maximized",
            "settings/window_mode_borderless",
            "settings/window_mode_fullscreen",
        };

        /// <summary>Frame rate caps in menu order; 0 = unlimited.</summary>
        public static readonly int[] FrameRates = { 0, 30, 60, 120, 144, 165, 240 };

        private static readonly Vector2I[] CommonSizes =
        {
            new(1280, 720),
            new(1280, 800),
            new(1366, 768),
            new(1440, 900),
            new(1600, 900),
            new(1680, 1050),
            new(1920, 1024),
            new(1920, 1080),
            new(1920, 1200),
            new(2560, 1080),
            new(2560, 1440),
            new(2560, 1600),
            new(3440, 1440),
            new(3840, 2160),
        };

        /// <summary>Menu index of a saved window mode; an unknown or minimized mode shows as windowed (index 0).</summary>
        public static int WindowModeIndex(int mode)
        {
            for (var i = 0; i < WindowModes.Length; i++)
            {
                if ((int)WindowModes[i] == mode)
                    return i;
            }
            return 0;
        }

        /// <summary>True for the mode whose window size the player chooses (windowed).</summary>
        public static bool UsesWindowSize(int mode) => mode == (int)DisplayServer.WindowMode.Windowed;

        /// <summary>Menu index of a saved frame rate cap; a value not in <see cref="FrameRates"/> shows as the nearest higher cap, above all of them as unlimited.</summary>
        public static int FrameRateIndex(int fps)
        {
            if (fps <= 0)
                return 0;
            for (var i = 1; i < FrameRates.Length; i++)
            {
                if (FrameRates[i] >= fps)
                    return i;
            }
            return 0;
        }

        /// <summary>
        /// Window sizes for a monitor of size <paramref name="screen"/>: the common
        /// sizes that fit, the monitor's own size and the <paramref name="saved"/>
        /// size when it fits, without repeats, sorted by width then height.
        /// </summary>
        public static List<Vector2I> WindowSizes(Vector2I screen, Vector2I saved)
        {
            var result = new List<Vector2I>();
            foreach (var size in CommonSizes)
                AddIfFits(result, size, screen);
            AddIfFits(result, screen, screen);
            AddIfFits(result, saved, screen);
            result.Sort(CompareSizes);
            return result;
        }

        /// <summary>
        /// Index of <paramref name="size"/> in <paramref name="sizes"/>, or of the
        /// largest size not bigger than it; 0 when every size is bigger.
        /// </summary>
        public static int WindowSizeIndex(List<Vector2I> sizes, Vector2I size)
        {
            var best = 0;
            for (var i = 0; i < sizes.Count; i++)
            {
                if (sizes[i] == size)
                    return i;
                if (sizes[i].X <= size.X && sizes[i].Y <= size.Y)
                    best = i;
            }
            return best;
        }

        /// <summary>
        /// Shrinks <paramref name="size"/> to fit <paramref name="area"/> (the
        /// monitor's usable rectangle: without the taskbar), so a saved window
        /// never opens bigger than the monitor it is moved to.
        /// </summary>
        public static Vector2I Fit(Vector2I size, Vector2I area)
        {
            return new Vector2I(Mathf.Clamp(size.X, 1, Mathf.Max(1, area.X)), Mathf.Clamp(size.Y, 1, Mathf.Max(1, area.Y)));
        }

        private static void AddIfFits(List<Vector2I> sizes, Vector2I size, Vector2I screen)
        {
            if (size.X <= 0 || size.Y <= 0 || size.X > screen.X || size.Y > screen.Y || sizes.Contains(size))
                return;
            sizes.Add(size);
        }

        private static int CompareSizes(Vector2I a, Vector2I b) => a.X != b.X ? a.X.CompareTo(b.X) : a.Y.CompareTo(b.Y);
    }
}
