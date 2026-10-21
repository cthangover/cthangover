#if TOOLS
namespace Cthangover.Core.Autotest
{
    using System;
    using System.Linq;
    using Cthangover.Core.Audio;
    using Cthangover.Core.Settings;
    using Cthangover.Core.Utils;
    using Godot;

    /// <summary>
    /// Integration coverage of the audio subsystem migrated into
    /// <c>audio_base</c>: the engine mod — not the core — provides the audio
    /// service and the music player at their well-known <c>/root</c> paths, and
    /// the service plays sounds and ambience from mod files without throwing.
    /// </summary>
    public partial class AudioCoverageTestSetup : Node
    {
        private const string Category = "AUDIO_COVERAGE";

        private int _passed;
        private int _failed;

        public override void _Ready() => Callable.From(Run).CallDeferred();

        private void Run()
        {
            var kernelAssembly = typeof(GameData).Assembly;
            var service = GetTree().Root.GetNodeOrNull("AudioService");

            Check("/root/AudioService is provided and implements IAudioService", service is IAudioService);
            Check("the service type comes from a mod assembly", service != null && service.GetType().Assembly != kernelAssembly);
            Check("/root/MusicPlayer is provided by a mod", GetTree().Root.GetNodeOrNull("MusicPlayer") is { } player && player.GetType().Assembly != kernelAssembly);
            Check("kernel has no AudioService type", !kernelAssembly.GetTypes().Any(t => t.Name == "AudioService"));

            if (service is IAudioService audio)
            {
                Check("a UI sound plays without throwing", Runs(() => audio.PlaySound("ui/click", SoundType.UI)));
                Check("ambience starts and stops without throwing", Runs(() => audio.PlayAmbient("bar/bar_ambient")) && Runs(audio.StopAmbient));
            }

            GameLogger.Log(Category, $"[TEST_RESULT] name=coverage_audio passed={_passed} failed={_failed}");
            GetTree().Quit(_failed == 0 ? 0 : 1);
        }

        private static bool Runs(Action action)
        {
            try
            {
                action();
                return true;
            }
            catch (Exception e)
            {
                GameLogger.Log(Category, $"action failed: {e.GetType().Name}: {e.Message}", LogLevel.Error);
                return false;
            }
        }

        private void Check(string what, bool ok)
        {
            if (ok)
            {
                _passed++;
                GameLogger.Log(Category, "PASS " + what);
                return;
            }
            _failed++;
            GameLogger.Log(Category, "FAIL " + what, LogLevel.Error);
        }
    }
}
#endif
