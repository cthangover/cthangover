#if TOOLS
namespace Cthangover.Core.Autotest
{
    using System.IO;
    using Cthangover.Core.Settings;
    using Cthangover.Core.Utils;
    using Godot;

    /// <summary>
    /// Runtime checks of <see cref="SaveService"/> against damaged slot files
    /// (a crash or a full disk mid-write leaves one behind): loading one must
    /// report failure to the menu that asked, never throw into it.
    /// </summary>
    public partial class SaveIntegrityTestSetup : Node
    {
        private const string Category = "SAVE_TEST";
        private const string BrokenSlot = SaveService.HiddenSlotPrefix + "test_broken";

        private int failed;

        public override void _Ready() => Callable.From(Run).CallDeferred();

        private void Run()
        {
            Directory.CreateDirectory(SaveService.Path);
            var path = Path.Combine(SaveService.Path, BrokenSlot + ".json");
            File.WriteAllText(path, "{\"SaveId\":\"test_broken\",\"ModData\":{\"__core__\":{\"Ver");

            try
            {
                Check("loading a truncated slot reports failure", !SaveService.Load(BrokenSlot));
            }
            catch (System.Exception ex)
            {
                Check($"loading a truncated slot threw {ex.GetType().Name}: {ex.Message}", false);
            }
            finally
            {
                File.Delete(path);
            }

            GameLogger.Log(Category, $"[TEST_RESULT] name=save_integrity passed={(failed == 0 ? 1 : 0)} failed={failed}");
            GetTree().Quit(failed == 0 ? 0 : 1);
        }

        private void Check(string what, bool ok)
        {
            if (ok)
            {
                GameLogger.Log(Category, $"PASS {what}");
                return;
            }
            failed++;
            GameLogger.Log(Category, $"FAIL {what}", LogLevel.Error);
        }
    }
}
#endif
