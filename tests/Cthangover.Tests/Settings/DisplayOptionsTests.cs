using System.Collections.Generic;
using Cthangover.Core.Settings;
using Godot;

namespace Engine.Tests.Settings
{
    /// <summary>
    /// Списки настроек экрана и сопоставление сохранённых значений с ними:
    /// режимы окна, ограничение кадров, размеры окна под монитор и подгонка
    /// размера под рабочую область монитора.
    /// </summary>
    public class DisplayOptionsTests
    {
        [Fact]
        public void WindowModes_HaveOneLocaleKeyEach_AndNeverOfferMinimized()
        {
            Assert.Equal(DisplayOptions.WindowModes.Length, DisplayOptions.WindowModeKeys.Length);
            Assert.DoesNotContain(DisplayServer.WindowMode.Minimized, DisplayOptions.WindowModes);
        }

        [Theory]
        [InlineData((int)DisplayServer.WindowMode.Windowed, 0)]
        [InlineData((int)DisplayServer.WindowMode.Maximized, 1)]
        [InlineData((int)DisplayServer.WindowMode.Fullscreen, 2)]
        [InlineData((int)DisplayServer.WindowMode.ExclusiveFullscreen, 3)]
        [InlineData((int)DisplayServer.WindowMode.Minimized, 0)]
        [InlineData(99, 0)]
        [InlineData(-1, 0)]
        public void WindowModeIndex_SavedMode_MapsToMenuItem(int mode, int expected)
        {
            Assert.Equal(expected, DisplayOptions.WindowModeIndex(mode));
        }

        [Fact]
        public void WindowModeIndex_EveryOfferedMode_RoundTrips()
        {
            for (var i = 0; i < DisplayOptions.WindowModes.Length; i++)
                Assert.Equal(i, DisplayOptions.WindowModeIndex((int)DisplayOptions.WindowModes[i]));
        }

        [Theory]
        [InlineData((int)DisplayServer.WindowMode.Windowed, true)]
        [InlineData((int)DisplayServer.WindowMode.Maximized, false)]
        [InlineData((int)DisplayServer.WindowMode.Fullscreen, false)]
        [InlineData((int)DisplayServer.WindowMode.ExclusiveFullscreen, false)]
        public void UsesWindowSize_OnlyWindowed(int mode, bool expected)
        {
            Assert.Equal(expected, DisplayOptions.UsesWindowSize(mode));
        }

        [Theory]
        [InlineData(0, 0)]
        [InlineData(-5, 0)]
        [InlineData(30, 1)]
        [InlineData(60, 2)]
        [InlineData(144, 4)]
        [InlineData(100, 3)]
        [InlineData(1000, 0)]
        public void FrameRateIndex_SavedCap_MapsToMenuItem(int fps, int expected)
        {
            Assert.Equal(expected, DisplayOptions.FrameRateIndex(fps));
        }

        [Fact]
        public void FrameRates_StartWithUnlimited()
        {
            Assert.Equal(0, DisplayOptions.FrameRates[0]);
        }

        [Fact]
        public void WindowSizes_FullHdMonitor_OnlySizesThatFit_SortedWithoutRepeats()
        {
            var sizes = DisplayOptions.WindowSizes(new Vector2I(1920, 1080), new Vector2I(1920, 1080));

            Assert.Contains(new Vector2I(1280, 720), sizes);
            Assert.Contains(new Vector2I(1920, 1080), sizes);
            Assert.DoesNotContain(new Vector2I(2560, 1440), sizes);
            Assert.DoesNotContain(new Vector2I(1920, 1200), sizes);
            Assert.Equal(sizes.Count, new HashSet<Vector2I>(sizes).Count);
            for (var i = 1; i < sizes.Count; i++)
                Assert.True(sizes[i - 1].X < sizes[i].X || (sizes[i - 1].X == sizes[i].X && sizes[i - 1].Y < sizes[i].Y), $"{sizes[i - 1]} before {sizes[i]}");
        }

        [Fact]
        public void WindowSizes_UncommonMonitor_OffersItsOwnSize()
        {
            var sizes = DisplayOptions.WindowSizes(new Vector2I(1760, 990), new Vector2I(0, 0));

            Assert.Equal(new Vector2I(1760, 990), sizes[^1]);
            Assert.Contains(new Vector2I(1280, 720), sizes);
        }

        [Fact]
        public void WindowSizes_SavedUncommonSize_StaysSelectable()
        {
            var saved = new Vector2I(1500, 850);

            var sizes = DisplayOptions.WindowSizes(new Vector2I(1920, 1080), saved);

            Assert.Contains(saved, sizes);
            Assert.Equal(saved, sizes[DisplayOptions.WindowSizeIndex(sizes, saved)]);
        }

        [Fact]
        public void WindowSizes_SavedSizeBiggerThanMonitor_IsNotOffered()
        {
            var sizes = DisplayOptions.WindowSizes(new Vector2I(1366, 768), new Vector2I(2560, 1440));

            Assert.DoesNotContain(new Vector2I(2560, 1440), sizes);
            Assert.Equal(new Vector2I(1366, 768), sizes[^1]);
        }

        [Fact]
        public void WindowSizes_TinyMonitor_StillOffersItsSize()
        {
            var sizes = DisplayOptions.WindowSizes(new Vector2I(1024, 600), new Vector2I(1920, 1080));

            Assert.Equal(new List<Vector2I> { new(1024, 600) }, sizes);
        }

        [Fact]
        public void WindowSizeIndex_SizeNotInList_PicksLargestNotBigger()
        {
            var sizes = new List<Vector2I> { new(1280, 720), new(1600, 900), new(1920, 1080) };

            Assert.Equal(1, DisplayOptions.WindowSizeIndex(sizes, new Vector2I(1700, 1000)));
            Assert.Equal(2, DisplayOptions.WindowSizeIndex(sizes, new Vector2I(1920, 1080)));
            Assert.Equal(0, DisplayOptions.WindowSizeIndex(sizes, new Vector2I(800, 600)));
        }

        [Fact]
        public void WindowSizeIndex_EmptyList_IsZero()
        {
            Assert.Equal(0, DisplayOptions.WindowSizeIndex(new List<Vector2I>(), new Vector2I(1920, 1080)));
        }

        [Theory]
        [InlineData(1280, 720, 1920, 1040, 1280, 720)]
        [InlineData(1920, 1080, 1920, 1040, 1920, 1040)]
        [InlineData(2560, 1440, 1366, 728, 1366, 728)]
        [InlineData(0, 0, 1920, 1040, 1, 1)]
        [InlineData(1280, 720, 0, 0, 1, 1)]
        public void Fit_ShrinksToUsableArea(int w, int h, int areaW, int areaH, int expectedW, int expectedH)
        {
            Assert.Equal(new Vector2I(expectedW, expectedH), DisplayOptions.Fit(new Vector2I(w, h), new Vector2I(areaW, areaH)));
        }
    }
}
