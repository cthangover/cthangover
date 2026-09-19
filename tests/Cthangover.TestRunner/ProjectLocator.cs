namespace Cthangover.TestRunner
{
    /// <summary>
    /// Finds the game project root by walking up from the current directory
    /// and, as a fallback, from the runner assembly location (covers
    /// <c>dotnet run</c> from any subdirectory and published binaries placed
    /// elsewhere). The root is identified by <c>project.godot</c> — the same
    /// anchor Godot itself uses via <c>--path</c>.
    /// </summary>
    public static class ProjectLocator
    {
        public static string Locate()
        {
            return FindFrom(Directory.GetCurrentDirectory())
                ?? FindFrom(AppContext.BaseDirectory)
                ?? throw new InvalidOperationException(
                    "project.godot not found: run the test runner from inside the CThangover repository");
        }

        private static string FindFrom(string start)
        {
            var directory = new DirectoryInfo(start);
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "project.godot")))
                    return directory.FullName;
                directory = directory.Parent;
            }
            return null;
        }
    }
}
