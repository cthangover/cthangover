using Cthangover.Core.Settings;
using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

#if WINDOWS
using System.Runtime.InteropServices;
#endif

namespace Cthangover.Core.Utils
{
    /// <summary>
    /// Central logging sink for the entire project. Every subsystem — the scenario
    /// engine, mod loader, UI framework, and AI behaviours — routes diagnostic
    /// output through this static class.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The logger writes to three destinations simultaneously when enabled:
    /// <list type="number">
    /// <item>
    /// <term>Console</term><description>A separate console window allocated
    /// via <c>AllocConsole</c> (Windows) or stdout (Linux/macOS) with
    /// colour-coded category labels. Controlled by
    /// <c>GameConfig.Instance.Logging.ConsoleEnabled</c>.</description>
    /// </item>
    /// <item>
    /// <term>Godot output</term><description>Falls back to <see cref="GD.Print"/>
    /// if a dedicated console is unavailable, so messages still appear in the
    /// Godot editor's Output panel.</description>
    /// </item>
    /// <item>
    /// <term>Log files</term><description>All entries go to
    /// <c>cthangover.log</c>; entries with <see cref="LogLevel.Error"/> are
    /// also duplicated to <c>cthangover_errors.log</c> for quick triage.</description>
    /// </item>
    /// </list>
    /// </para>
    /// <para>
    /// Initialisation is lazy and implicit: the first call to <see cref="Log"/>
    /// triggers <see cref="Init"/> automatically, which reads configuration from
    /// <see cref="Cthangover.Core.Settings.GameConfig.Instance"/>.
    /// </para>
    /// </remarks>
    public static class GameLogger
    {
#if WINDOWS
        [DllImport("kernel32.dll")]
        private static extern bool AllocConsole();

        [DllImport("kernel32.dll")]
        private static extern bool FreeConsole();
#endif

        private static string _logPath;
        private static string _errorLogPath;
        private static bool _initialized;
        private static bool _enabled = true;
        private static bool _consoleAllocated;
        private static LogLevel _minimumLevel = LogLevel.Debug;
        private static HashSet<string> _enabledCategories;
        private static readonly object _lock = new();
        
        /// <summary>
        /// Accumulates compilation errors emitted by the scenario scripting
        /// subsystem during source parsing. Other modules may inspect this list
        /// after a batch compile to surface diagnostics to the user.
        /// </summary>
        public static readonly List<string> CompilationErrors = new();
        
        /// <summary>
        /// Explicit initialisation entry point. Reads logging configuration
        /// from <see cref="Cthangover.Core.Settings.GameConfig.Instance"/>,
        /// ensures the <c>logs/</c> directory exists under the Godot user-data
        /// folder, honours the <c>--log-file=</c> command-line override, and
        /// optionally allocates a Windows console. Idempotent — subsequent
        /// calls are no-ops.
        /// </summary>
        public static void Init()
        {
            if (_initialized)
                return;

            _initialized = true;

            try
            {
                if (!IsGodotAvailable())
                {
                    _enabled = false;
                    return;
                }

                _enabled = GameConfig.Instance.Logging.Enabled;

                _minimumLevel = ParseLogLevel(GameConfig.Instance.Logging.MinimumLevel);
                var enabledCategories = GameConfig.Instance.Logging.EnabledCategories;
                if (enabledCategories != null && enabledCategories.Count > 0)
                    _enabledCategories = new HashSet<string>(enabledCategories, StringComparer.OrdinalIgnoreCase);

                var userDir = OS.GetUserDataDir();
                var logsDir = Path.Combine(userDir, "logs");
                DirAccess.MakeDirRecursiveAbsolute(logsDir);

                _logPath = Path.Combine(logsDir, "cthangover.log");
                _errorLogPath = Path.Combine(logsDir, "cthangover_errors.log");

                var args = OS.GetCmdlineArgs();
                foreach (var arg in args)
                {
                    if (arg.StartsWith("--log-file="))
                    {
                        _logPath = arg.Substring("--log-file=".Length);
                        break;
                    }
                }

                if (GameConfig.Instance.Logging.ConsoleEnabled)
                {
#if WINDOWS
                    try
                    {
                        _consoleAllocated = AllocConsole();
                    }
                    catch
                    {
                        _consoleAllocated = false;
                    }
#else
                    _consoleAllocated = true;
#endif
                }

                Log("LOGGER", $"Logger initialized, path={_logPath}");
            }
            catch
            {
                _enabled = false;
            }
        }

        private static bool IsGodotAvailable()
        {
            try
            {
                var _ = Godot.Engine.GetMainLoop();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsModsCategory(string category)
        {
            return category.StartsWith("MODS");
        }

        private static LogLevel ParseLogLevel(string value)
        {
            if (string.IsNullOrEmpty(value))
                return LogLevel.Debug;
            if (Enum.TryParse<LogLevel>(value, true, out var result))
                return result;
            return LogLevel.Debug;
        }

#if WINDOWS
        private static ConsoleColor GetCategoryColor(string category, LogLevel level)
        {
            return level switch
            {
                LogLevel.Error => ConsoleColor.DarkRed,
                LogLevel.Warning => ConsoleColor.DarkYellow,
                LogLevel.Debug => ConsoleColor.DarkGray,
                LogLevel.Message when IsModsCategory(category) => ConsoleColor.Yellow,
                _ => ConsoleColor.Gray
            };
        }

        private static void WriteColoredWindows(string timestamp, string category, string message, LogLevel level)
        {
            var categoryColor = GetCategoryColor(category, level);
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.Write($"[{timestamp}] ");
            Console.ForegroundColor = categoryColor;
            Console.Write($"[{category}]");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($" {message}");
        }
#endif

        private static string GetAnsiColorCode(string category, LogLevel level)
        {
            return level switch
            {
                LogLevel.Error => "\x1b[31m",
                LogLevel.Warning => "\x1b[33m",
                LogLevel.Debug => "\x1b[90m",
                LogLevel.Message when IsModsCategory(category) => "\x1b[33m",
                _ => "\x1b[37m"
            };
        }

        private static void WriteColoredAnsi(string timestamp, string category, string message, LogLevel level)
        {
            var color = GetAnsiColorCode(category, level);
            Console.WriteLine($"[{timestamp}] {color}[{category}]\x1b[0m {message}");
        }

        /// <summary>
        /// Writes a timestamped, categorised entry to all active sinks (console,
        /// Godot output, and log files) provided the entry's <paramref name="level"/>
        /// meets the configured <c>MinimumLevel</c> threshold. Thread-safe: file
        /// I/O is synchronised with a private lock object.
        /// </summary>
        /// <param name="category">
        /// A short label identifying the source subsystem. Conventionally
        /// uppercase (e.g. <c>"MODS"</c>, <c>"SCENE"</c>). Displayed in
        /// colour on the console.
        /// </param>
        /// <param name="message">The free-form diagnostic text.</param>
        /// <param name="level">
        /// Severity classification. Defaults to <see cref="LogLevel.Message"/>.
        /// Errors are duplicated to the dedicated error log.
        /// </param>
        public static void Log(string category, string message, LogLevel level = LogLevel.Message)
        {
            if (!_initialized)
                Init();

            if (level < _minimumLevel)
                return;

            //if (_enabledCategories != null && !_enabledCategories.Contains(category))
            //    return;

            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var line = $"[{timestamp}] [{category}] {message}";

            if (_consoleAllocated)
            {
                try
                {
#if WINDOWS
                    WriteColoredWindows(timestamp, category, message, level);
#else
                    WriteColoredAnsi(timestamp, category, message, level);
#endif
                }
                catch
                {
                }
            }
            else if (_initialized)
            {
                try
                {
                    GD.Print(line);
                }
                catch
                {
                }
            }

            if (!_initialized || !_enabled)
                return;

            lock (_lock)
            {
                if (_logPath != null)
                    writeToFile(_logPath, line);
                
                if (_errorLogPath != null && level == LogLevel.Error)
                    writeToFile(_errorLogPath, line);
            }
        }

        private static void writeToFile(string fileName, string line)
        {
            try
            {
                using var stream = new FileStream(fileName, FileMode.Append, System.IO.FileAccess.Write, FileShare.Read);
                using var writer = new StreamWriter(stream, Encoding.UTF8);
                writer.WriteLine(line);
            }
            catch
            {
            }
        }
        
    }
}
