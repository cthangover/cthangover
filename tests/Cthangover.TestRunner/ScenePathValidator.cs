namespace Cthangover.TestRunner
{
    /// <summary>
    /// Checks a manifest <c>res://</c> scene path against the file system with
    /// exact casing before Godot is started. Windows resolves a wrong-case path
    /// silently, Linux/CI and exported packs do not — there the scene simply
    /// fails to open and the run ends with "no result marker". Validating up
    /// front turns that into a precise message on every OS.
    /// </summary>
    public static class ScenePathValidator
    {
        /// <summary>
        /// Returns null when <paramref name="resPath"/> exists under
        /// <paramref name="root"/> with exactly this casing; otherwise a
        /// human-readable problem, naming the on-disk spelling when only the
        /// case differs.
        /// </summary>
        public static string Check(string root, string resPath)
        {
            if (string.IsNullOrWhiteSpace(resPath))
                return "scene path is empty";
            if (!resPath.StartsWith("res://", StringComparison.Ordinal))
                return null; // uid:// and absolute paths are left to Godot

            var current = root;
            var segments = resPath.Substring("res://".Length).Split('/', StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < segments.Length; i++)
            {
                var segment = segments[i];
                var isLast = i == segments.Length - 1;
                var entries = isLast
                    ? Directory.EnumerateFileSystemEntries(current)
                    : Directory.EnumerateDirectories(current);

                string exact = null, caseOnly = null;
                foreach (var entry in entries)
                {
                    var name = Path.GetFileName(entry);
                    if (string.Equals(name, segment, StringComparison.Ordinal))
                    {
                        exact = name;
                        break;
                    }
                    if (string.Equals(name, segment, StringComparison.OrdinalIgnoreCase))
                        caseOnly = name;
                }

                if (exact == null)
                {
                    return caseOnly != null
                        ? $"scene path case mismatch: '{segment}' is '{caseOnly}' on disk ({resPath})"
                        : $"scene not found: {resPath}";
                }
                current = Path.Combine(current, exact);
            }
            return null;
        }
    }
}
