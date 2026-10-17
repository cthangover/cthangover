using System;
using System.IO;
using System.Text.Json;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Settings
{
    /// <summary>
    /// The last game and save the player touched (<c>user://last_session.json</c>),
    /// recorded on every save and load of a game collection. Drives the main
    /// menu's "Continue" from any mode — including the front-end, which then
    /// restarts into that game with <c>--load</c>.
    /// </summary>
    public static class LastSessionStore
    {
        private const string FilePath = "user://last_session.json";

        public sealed class Entry
        {
            public string GameId { get; set; }

            public string FileName { get; set; }

            public DateTime Time { get; set; }
        }

        /// <summary>Remembers the save; ignored for saves made without a game collection.</summary>
        public static void Record(string gameId, string fileName)
        {
            if (string.IsNullOrEmpty(gameId) || string.IsNullOrEmpty(fileName) || !GameEnvironment.IsRuntime)
                return;

            try
            {
                var json = JsonSerializer.Serialize(new Entry { GameId = gameId, FileName = fileName, Time = DateTime.UtcNow });
                File.WriteAllText(ProjectSettings.GlobalizePath(FilePath), json);
            }
            catch (Exception ex)
            {
                GameLogger.Log("SAVE", $"Could not record the last session: {ex.Message}", LogLevel.Warning);
            }
        }

        /// <summary>The last session whose save still exists, or null.</summary>
        public static Entry Load()
        {
            if (!GameEnvironment.IsRuntime)
                return null;

            try
            {
                var path = ProjectSettings.GlobalizePath(FilePath);
                if (!File.Exists(path))
                    return null;

                var entry = JsonSerializer.Deserialize<Entry>(File.ReadAllText(path));
                return entry != null && SaveService.ReadPackage(entry.GameId, entry.FileName) != null ? entry : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
