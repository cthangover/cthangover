namespace Cthangover.Core.Mods.Testing
{
    using System;
    using System.IO;
    using Cthangover.Core.Utils;
    using Godot;

    /// <summary>Verifies the runner's private user and project roots before settings, caches or game files are touched.</summary>
    public static class TestRuntimeEnvironment
    {
        public static void Verify()
        {
            var expected = System.Environment.GetEnvironmentVariable("CTHANGOVER_TEST_DATA");
            if (expected == null || !GameEnvironment.IsRuntime) return;
            Check(expected, OS.GetUserDataDir(), "user data");
            var project = System.Environment.GetEnvironmentVariable("CTHANGOVER_TEST_PROJECT");
            if (project == null) throw new InvalidOperationException("test project root is missing");
            Check(project, ProjectSettings.GlobalizePath("res://"), "project");
        }

        private static void Check(string expected, string actual, string name)
        {
            var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (!string.Equals(Path.GetFullPath(expected).TrimEnd('/', '\\'), Path.GetFullPath(actual).TrimEnd('/', '\\'), comparison))
                throw new InvalidOperationException($"Test {name} isolation mismatch: expected '{expected}', actual '{actual}'");
        }
    }
}
