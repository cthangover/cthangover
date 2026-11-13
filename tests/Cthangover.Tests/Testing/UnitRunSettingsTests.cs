namespace Cthangover.Tests.Testing
{
    using System;
    using System.IO;
    using System.Xml.Linq;
    using Cthangover.TestRunner;

    /// <summary>The outer scheduler controls parallelism; adapter settings serialize collections without filtering assertions.</summary>
    public class UnitRunSettingsTests : IDisposable
    {
        private readonly string directory = Path.Combine(Path.GetTempPath(), "unit_settings_" + Guid.NewGuid().ToString("N"));

        [Fact]
        public void SettingsDisableOnlyNestedCollectionParallelism()
        {
            var path = UnitRunSettings.Write(directory);
            Assert.Equal(Path.Combine(directory, "unit.runsettings"), path);
            var root = XDocument.Load(path).Root;
            Assert.Equal("RunSettings", root.Name.LocalName);
            var settings = Assert.Single(Assert.Single(root.Elements()).Elements());
            Assert.Equal("ParallelizeTestCollections", settings.Name.LocalName);
            Assert.Equal("false", settings.Value);
        }

        public void Dispose()
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}
