namespace Cthangover.TestRunner
{
    /// <summary>
    /// Resolves the Godot .NET executable for the current OS. Priority:
    /// explicit <c>--godot</c> argument, <c>GODOT_BIN</c> environment
    /// variable, then the bare name so the OS resolves it from PATH. Keeping
    /// the bare name (with the .exe suffix on Windows) is what makes the
    /// runner portable between developer machines and CI images.
    /// </summary>
    public static class GodotLocator
    {
        public static string Locate(string explicitPath)
        {
            if (!string.IsNullOrWhiteSpace(explicitPath))
                return explicitPath;
            var fromEnvironment = Environment.GetEnvironmentVariable("GODOT_BIN");
            if (!string.IsNullOrWhiteSpace(fromEnvironment))
                return fromEnvironment;
            return OperatingSystem.IsWindows() ? "godot.exe" : "godot";
        }
    }
}
