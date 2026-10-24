using Cthangover.Core.Events;
using Cthangover.Core.Mods;
using Cthangover.Core.Utils;

namespace Cthangover.Mods.Audio
{
    /// <summary>
    /// Engine-mod initializer for the audio subsystem. Registers the
    /// <c>/root/AudioService</c> and <c>/root/MusicPlayer</c> nodes when mod
    /// resources become ready and subscribes the music player to scene events,
    /// replacing the former core autoloads. Both operations are idempotent:
    /// <c>ModManager.Reload()</c> re-runs <c>OnModResourcesReady</c>, so node
    /// creation and event subscription must tolerate repeat invocations.
    /// </summary>
    public class AudioModInitializer : IModInitializer
    {
        private static bool subscribed;

        /// <summary>No per-mod setup is required; node registration happens on resources-ready.</summary>
        public void OnModLoaded(string modId) { }

        /// <summary>
        /// Ensures the audio nodes exist under the tree root and that the
        /// music player listens to scene events. Safe to call again after
        /// a mod reload.
        /// </summary>
        public void OnModResourcesReady()
        {
            EnsureAudioNodes();
            EnsureEventSubscriptions();
        }

        private static bool nodesQueued;

        private static void EnsureAudioNodes()
        {
            var root = ModEventBus.Instance?.GetTree()?.Root;
            if (root == null)
            {
                GameLogger.Log("AUDIO", "AudioModInitializer: tree root not available, skipping node setup", LogLevel.Error);
                return;
            }

            if (root.GetNodeOrNull("AudioService") != null || nodesQueued)
                return;
            nodesQueued = true;

            // Deferred: OnModResourcesReady fires while the root is still
            // setting up autoload children, so a direct AddChild is rejected.
            // Deferred calls run in FIFO order, which keeps AudioService ahead
            // of MusicPlayerBehaviour — its OnReady resolves AudioService.
            ModRoot.AddDeferred(new AudioService { Name = "AudioService" });
            ModRoot.AddDeferred(new MusicPlayerBehaviour { Name = "MusicPlayer" });
        }

        private static void EnsureEventSubscriptions()
        {
            if (subscribed)
                return;
            subscribed = true;

            ModEventBus.Subscribe<SceneEnteredEvent>(OnSceneEntered, priority: 10);
            ModEventBus.Subscribe<SceneRouteActivatingEvent>(OnRouteActivating);
        }

        private static void OnSceneEntered(SceneEnteredEvent evt)
        {
            NotifyMusicPlayers(evt.SceneName);
        }

        private static void OnRouteActivating(SceneRouteActivatingEvent evt)
        {
            NotifyMusicPlayers(evt.MusicScene);
        }

        private static void NotifyMusicPlayers(string musicScene)
        {
            if (string.IsNullOrEmpty(musicScene))
                return;

            var tree = ModEventBus.Instance?.GetTree();
            if (tree == null)
                return;

            foreach (var node in tree.GetNodesInGroup("music_player"))
            {
                if (node is MusicPlayerBehaviour player)
                {
                    GameLogger.Log("AUDIO", $"MusicPlayer notified about scene '{musicScene}'");
                    player.UpdateMusicType(musicScene);
                }
            }
        }
    }
}
