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
        
        public static readonly List<string> CompilationErrors = new();
        
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
