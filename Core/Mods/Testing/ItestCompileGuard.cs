using System.Linq;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Mods.Testing
{
    /// <summary>
    /// Fails an <c>--itest=</c> run at once when a mod of its set did not compile.
    /// Without it the suite's driver (or the mod it checks) is simply missing, nothing
    /// ever prints a result and the runner waits for its timeout with "no result marker",
    /// while the reason sits only in the user log. Called by <c>Boot</c> when the boot
    /// completes: logs every compilation error, prints a failing <c>[TEST_RESULT]</c>
    /// and quits with exit code 1. Runs without a mounted test suite are left alone —
    /// the main menu shows the errors to a player.
    /// </summary>
    public static class ItestCompileGuard
    {
        private const string ItestPrefix = "--itest=";

        /// <summary>True when the run was failed and the tree is quitting.</summary>
        public static bool FailIfBroken(SceneTree tree)
        {
            if (tree == null || ModTestSuites.ActiveIds.Count == 0 || GameLogger.CompilationErrors.Count == 0)
                return false;

            var name = Games.GameLaunchArgs.Current.UserArgs
                .FirstOrDefault(a => a.StartsWith(ItestPrefix, System.StringComparison.Ordinal))
                ?.Substring(ItestPrefix.Length) ?? ModTestSuites.ActiveIds[0];

            foreach (var error in GameLogger.CompilationErrors)
                GameLogger.Log("TEST", $"Mod compilation failed: {error}", LogLevel.Error);

            var count = GameLogger.CompilationErrors.Count;
            GameLogger.Log("TEST", $"Integration test '{name}' aborted: {count} mod compilation error(s)", LogLevel.Error);
            GameLogger.Log("TEST", $"[TEST_RESULT] name={name} passed=0 failed={count}");
            tree.Quit(1);
            return true;
        }
    }
}
