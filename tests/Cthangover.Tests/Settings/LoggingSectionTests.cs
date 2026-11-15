namespace Engine.Tests.Settings
{
    using System.Text.Json;
    using Cthangover.Core.Settings.Configs;

    public sealed class LoggingSectionTests
    {
        [Fact]
        public void DefaultsKeepTenMegabytesAndThreeFiles()
        {
            var config = JsonSerializer.Deserialize<LoggingSection>("{}");
            Assert.Equal(10, config.MaxFileSizeMb);
            Assert.Equal(3, config.MaxFiles);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(int.MinValue)]
        public void InvalidLimitsClampToOneInPropertiesAndJson(int value)
        {
            var config = new LoggingSection { MaxFileSizeMb = value, MaxFiles = value };
            Assert.Equal(1, config.MaxFileSizeMb);
            Assert.Equal(1, config.MaxFiles);
            config = JsonSerializer.Deserialize<LoggingSection>("{\"max_file_size_mb\":" + value + ",\"max_files\":" + value + "}");
            Assert.Equal(1, config.MaxFileSizeMb);
            Assert.Equal(1, config.MaxFiles);
        }

        [Fact]
        public void EveryLoggingPreferenceSurvivesJsonRoundTrip()
        {
            var original = new LoggingSection
            {
                Enabled = false, ConsoleEnabled = true, MinimumLevel = "warning",
                EnabledCategories = new() { "BOOT", "MODS" }, MaxFileSizeMb = 7, MaxFiles = 2
            };
            var restored = JsonSerializer.Deserialize<LoggingSection>(JsonSerializer.Serialize(original));
            Assert.Equal(original.Enabled, restored.Enabled);
            Assert.Equal(original.ConsoleEnabled, restored.ConsoleEnabled);
            Assert.Equal(original.MinimumLevel, restored.MinimumLevel);
            Assert.Equal(original.EnabledCategories, restored.EnabledCategories);
            Assert.Equal(original.MaxFileSizeMb, restored.MaxFileSizeMb);
            Assert.Equal(original.MaxFiles, restored.MaxFiles);
        }
    }
}
