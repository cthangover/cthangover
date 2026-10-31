namespace Cthangover.Core.Mods
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.Json;
    using Cthangover.Core.Utils;

    /// <summary>
    /// Explicitly remembered archive passwords, keyed by absolute archive path rather than
    /// untrusted manifest ids. The local JSON file stores plaintext secrets; the UI explains
    /// that before saving. Paths follow the host's case rules. No password is logged.
    /// </summary>
    public sealed class ArchivePasswordStore
    {
        private readonly string filePath;
        private readonly object gate = new();
        private readonly Dictionary<string, string> session = new(
            OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        private static ArchivePasswordStore defaultStore;

        /// <summary>Uses an explicit storage file, also allowing isolated tests without Godot.</summary>
        public ArchivePasswordStore(string filePath) => this.filePath = Path.GetFullPath(filePath);

        /// <summary>Resolve Godot paths on the main thread before background discovery.</summary>
        public static void WarmUp() => defaultStore = new ArchivePasswordStore(
            GameEnvironment.IsRuntime ? GamePaths.Globalize("user://archive_passwords.json") : "archive_passwords.json");

        /// <summary>Shared user store; boot warms it before any worker accesses it.</summary>
        public static ArchivePasswordStore Default => defaultStore ??= new ArchivePasswordStore("archive_passwords.json");

        /// <summary>Returns the remembered password, or null when this archive has none.</summary>
        public string Get(string archivePath)
        {
            lock (gate)
            {
                var key = Path.GetFullPath(archivePath);
                if (session.TryGetValue(key, out var current))
                    return current;
                return Load().TryGetValue(key, out var password) ? password : null;
            }
        }

        /// <summary>Retains a password only in process memory. Null forgets the session override.</summary>
        public void SetSession(string archivePath, string password)
        {
            lock (gate)
            {
                var key = Path.GetFullPath(archivePath);
                if (password == null)
                    session.Remove(key);
                else
                    session[key] = password;
            }
        }

        /// <summary>
        /// Saves a password atomically; null removes it. Empty passwords remain valid values.
        /// On Unix the file and the temporary file are accessible only to the current user.
        /// </summary>
        public void Set(string archivePath, string password)
        {
            lock (gate)
            {
                var values = Load();
                var key = Path.GetFullPath(archivePath);
                if (password == null)
                    values.Remove(key);
                else
                    values[key] = password;
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                var temporary = filePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
                    if (!OperatingSystem.IsWindows())
                        options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
                    using (var stream = new FileStream(temporary, options))
                        JsonSerializer.Serialize(stream, values);
                    File.Move(temporary, filePath, true);
                    session.Remove(key);
                }
                finally
                {
                    if (File.Exists(temporary))
                        File.Delete(temporary);
                }
            }
        }

        private Dictionary<string, string> Load()
        {
            var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
            if (!File.Exists(filePath))
                return new Dictionary<string, string>(comparer);
            // Broken/unreadable credential files must not silently get overwritten.
            var values = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(filePath));
            return new Dictionary<string, string>(values ?? new Dictionary<string, string>(), comparer);
        }
    }
}
