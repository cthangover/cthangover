namespace Cthangover.Core.Bootstrap
{
    using System;

    /// <summary>
    /// Godot tools choose their CLR before loading the game assembly, so the
    /// project's runtimeconfig cannot enforce their minimum. Reject the old
    /// collectible-ALC implementation before any mod can be compiled or unloaded.
    /// Self-contained releases pin their CLR servicing version in Cthangover.csproj.
    /// </summary>
    public static class RuntimeRequirements
    {
        /// <summary>Returns the actionable startup error, or null for a fixed CLR.</summary>
        public static string GetProblem(Version version)
            => version != null && version.Major >= 10 ? null
                : $".NET runtime {version?.ToString() ?? "unknown"} is unsupported: mod unloading requires .NET 10. "
                    + "Install the .NET 10 SDK and use Godot 4.6.3 .NET "
                    + "with the net10.0 project. No .NET 8 installation is required.";
    }
}
