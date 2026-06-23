using System;

namespace Cthangover.Core.Settings
{
    public class SaveSlotInfo
    {
        public string FileName { get; set; }
        public DateTime SaveTime { get; set; }
        public string SceneName { get; set; }
        public string ScreenshotPath { get; set; }
        public bool HasScreenshot { get; set; }
        public bool IsEmpty { get; set; }
    }
}
