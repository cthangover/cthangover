namespace Cthangover.Mods.ExecutionBootstrap
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.Json;
    using Cthangover.Core.Bootstrap;
    using Cthangover.Core.Mods.Testing;
    using Cthangover.Core.Settings;
    using Cthangover.Core.Utils;
    using Godot;

    /// <summary>
    /// Checks the actual no-context runtime, default logger roots and native settings round trip. The external
    /// parent owns process exit/shutdown verification; this receipt and terminal marker alone never establish PASS.
    /// </summary>
    public partial class ExecutionBootstrapDriver : Node
    {
        private bool finished;
        private double elapsed;
        private string output;
        private string token;
        private string expectedData;

        /// <summary>Runs once after boot; a bounded readiness failure still exits through normal application cleanup.</summary>
        public override void _Process(double delta)
        {
            if (finished)
            {
                return;
            }

            elapsed += delta;
            if (!Boot.IsReady && elapsed < 30)
            {
                return;
            }

            finished = true;
            var exitCode = 1;
            try
            {
                ReadArguments();
                Require(Boot.IsReady, "Boot did not become ready within30seconds");
                Execute();
                exitCode = 0;
            }
            catch (Exception error)
            {
                GameLogger.Log("TEST", "[EXECUTION_BOOTSTRAP_FAILURE] " + error, LogLevel.Error);
            }
            finally
            {
                GameLogger.Log("TEST", "[TEST_RESULT] name=test_execution_bootstrap passed="
                    + (exitCode == 0 ? "1 failed=0" : "0 failed=1"));
                ApplicationExit.Quit(GetTree(), exitCode);
            }
        }

        private void ReadArguments()
        {
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var argument in OS.GetCmdlineUserArgs())
            {
                const string prefix = "--execution-bootstrap-";
                if (!argument.StartsWith(prefix, StringComparison.Ordinal))
                {
                    continue;
                }

                var separator = argument.IndexOf('=');
                Require(separator > prefix.Length, "Bootstrap arguments require explicit =values");
                var key = argument.Substring(prefix.Length, separator - prefix.Length);
                Require(key is "output" or "token" or "data", "Unknown bootstrap argument");
                Require(values.TryAdd(key, argument[(separator + 1)..]), "Duplicate bootstrap argument");
            }

            Require(values.Count == 3, "Bootstrap output/token/data arguments are required");
            output = Absolute(values["output"]);
            expectedData = Absolute(values["data"]);
            token = values["token"];
            Require(token.Length is > 0 and <= 128, "Bootstrap token length is invalid");
            foreach (var character in token)
            {
                Require(char.IsAsciiLetterOrDigit(character) || character is '_' or '-', "Bootstrap token is not safe");
            }

            Require(!Overlaps(output, ProjectSettings.GlobalizePath("res://")) && !Overlaps(output, expectedData),
                "Bootstrap evidence must be outside project inputs and user data");
        }

        private void Execute()
        {
            Require(TestExecutionContextRuntime.Current == null, "Ordinary runtime activated a test context");
            foreach (System.Collections.DictionaryEntry entry in System.Environment.GetEnvironmentVariables())
            {
                Require(!((string)entry.Key).StartsWith("CTHANGOVER_TEST_", StringComparison.OrdinalIgnoreCase),
                    "Normal bootstrap inherited test environment");
            }

            var data = Absolute(OS.GetUserDataDir());
            Require(SamePath(expectedData, data), "Native user data differs from the isolated expected root");
            var settingsPath = Absolute(ProjectSettings.GlobalizePath("user://settings.json"));
            Require(SamePath(settingsPath, Path.Combine(data, "settings.json")), "Settings escaped native user data");
            var settings = GameData.Instance.Settings;
            var originalSpeed = settings.BattleSpeed;
            const int savedSpeed = 13;
            var loadedSpeed = -1;
            try
            {
                settings.BattleSpeed = savedSpeed;
                settings.Save();
                using (var saved = JsonDocument.Parse(File.ReadAllText(settingsPath)))
                {
                    Require(saved.RootElement.GetProperty("BattleSpeed").GetInt32() == savedSpeed,
                        "Settings file did not contain the saved value");
                }

                settings.BattleSpeed = 27;
                settings.Load();
                loadedSpeed = settings.BattleSpeed;
                Require(loadedSpeed == savedSpeed, "Settings.Load did not restore the actual saved value");
            }
            finally
            {
                settings.BattleSpeed = originalSpeed;
                settings.Save();
            }

            var info = "[EXECUTION_BOOTSTRAP_INFO] token=" + token;
            var error = "[EXECUTION_BOOTSTRAP_EXPECTED_ERROR] token=" + token;
            GameLogger.Log("TEST", info);
            GameLogger.Log("TEST", error, LogLevel.Error);
            var mainPath = Path.Combine(data, "logs", "cthangover.log");
            var errorPath = Path.Combine(data, "logs", "cthangover_errors.log");
            var main = ReadLiveLog(mainPath);
            var errors = ReadLiveLog(errorPath);
            Require(main.Contains(info, StringComparison.Ordinal) && main.Contains(error, StringComparison.Ordinal),
                "Default main log did not contain both tokens");
            Require(!errors.Contains(info, StringComparison.Ordinal) && errors.Contains(error, StringComparison.Ordinal),
                "Default error sink did not preserve severity separation");
            Require(!main.Contains("[TEST_EXECUTION_READY]", StringComparison.Ordinal), "Ordinary runtime emitted context READY");
            var record = new Dictionary<string, object>
            {
                ["schemaVersion"] = 1,
                ["phase"] = "verified",
                ["mode"] = "normal",
                ["token"] = token,
                ["osPid"] = System.Environment.ProcessId,
                ["contextActivated"] = false,
                ["projectRoot"] = Absolute(ProjectSettings.GlobalizePath("res://")),
                ["userDataRoot"] = data,
                ["temporaryRoot"] = Absolute(Path.GetTempPath()),
                ["mainLogPath"] = mainPath,
                ["errorLogPath"] = errorPath,
                ["settingsPath"] = settingsPath,
                ["savedBattleSpeed"] = savedSpeed,
                ["loadedBattleSpeed"] = loadedSpeed,
                ["restoredBattleSpeed"] = originalSpeed
            };
            Publish(record);
        }

        private static string ReadLiveLog(string path)
        {
            using var stream = new FileStream(path, FileMode.Open, System.IO.FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        private void Publish(Dictionary<string, object> record)
        {
            Directory.CreateDirectory(output);
            var pending = Path.Combine(output, "bootstrap.json.tmp");
            var path = Path.Combine(output, "bootstrap.json");
            try
            {
                using (var stream = new FileStream(pending, FileMode.CreateNew, System.IO.FileAccess.Write, FileShare.Read))
                {
                    JsonSerializer.Serialize(stream, record);
                    stream.Flush(true);
                }

                File.Move(pending, path);
            }
            finally
            {
                if (File.Exists(pending))
                {
                    File.Delete(pending);
                }
            }
        }

        private static string Absolute(string path)
        {
            Require(Path.IsPathFullyQualified(path), "Bootstrap paths must be absolute");
            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        }

        private static bool SamePath(string first, string second)
        {
            return string.Equals(Absolute(first), Absolute(second), OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        }

        private static bool Overlaps(string first, string second)
        {
            if (SamePath(first, second))
            {
                return true;
            }

            return Within(first, second) || Within(second, first);
        }

        private static bool Within(string parent, string child)
        {
            var relative = Path.GetRelativePath(Absolute(parent), Absolute(child));
            return !Path.IsPathRooted(relative) && relative != ".."
                && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
