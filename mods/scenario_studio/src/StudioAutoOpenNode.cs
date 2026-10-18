namespace Cthangover.Mods.ScenarioStudio
{
    using Cthangover.Core.Bootstrap;
    using Cthangover.Core.Scenes;
    using Cthangover.Core.UI;
    using Cthangover.Core.Utils;
    using Godot;

    /// <summary>
    /// Waits until boot is done and the game shell (its <c>TopView</c> toolbar)
    /// is in the tree, toggles the <c>studio</c> UI entry exactly like the
    /// toolbar button, then frees itself. Started by <see cref="StudioLaunchInitializer"/>.
    /// </summary>
    public partial class StudioAutoOpenNode : Node
    {
        private const string EntryId = "studio";
        private const float PollSeconds = 0.5f;

        public StudioAutoOpenNode()
        {
            var timer = new Timer { WaitTime = PollSeconds, Autostart = true };
            AddChild(timer);
            timer.Timeout += OnPoll;
        }

        private void OnPoll()
        {
            if (!Boot.IsReady || UiHost.Instance == null || SceneContextNode.FindNode<Node>("TopView") == null)
                return;

            AppServices.Current.Ui.ToggleEntry(EntryId);
            GameLogger.Log("UI", "Scenario studio: dock opened by --studio");
            QueueFree();
        }
    }
}
