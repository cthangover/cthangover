namespace Cthangover.Mods.ScenarioStudio
{
    using System.Collections.Generic;
    using Cthangover.Core.Events;
    using Cthangover.Core.Scenes;
    using Godot;

    /// <summary>
    /// What happened in the session, for the "Log", "Scene" and "Map" tabs:
    /// log entries (scene entries, skipped candidates with the reason, scenario
    /// starts and ends, breakpoints, studio edits), the order of visited scenes
    /// and the last composition report of every scene. Fed by the mod event
    /// bus and by <see cref="StudioLive"/>; times are session time (mm:ss since
    /// the studio started).
    /// </summary>
    public sealed class StudioJournal
    {
        private const int MaxEntries = 300;

        private readonly ulong _startMsec = Time.GetTicksMsec();
        private readonly Dictionary<string, SceneComposedEvent> _composed = new();

        public List<StudioLogEntry> Entries { get; } = new();

        /// <summary>Scenes in the order they were entered (repeats included).</summary>
        public List<string> SceneHistory { get; } = new();

        public void Add(StudioLogCategory category, StudioText text, bool warning = false)
        {
            Entries.Add(new StudioLogEntry { Time = Now(), Category = category, Text = text, IsWarning = warning });
            if (Entries.Count > MaxEntries)
                Entries.RemoveAt(0);
        }

        /// <summary>Last composition report of <paramref name="scene"/>, or null.</summary>
        public SceneComposedEvent Composed(string scene) =>
            scene != null && _composed.TryGetValue(scene, out var report) ? report : null;

        public void OnSceneEntered(SceneEnteredEvent e)
        {
            SceneHistory.Add(e.SceneName);
            Add(StudioLogCategory.Scene, new StudioText("studio/log_text_enter", e.SceneName));
        }

        public void OnSceneComposed(SceneComposedEvent e)
        {
            _composed[e.SceneName] = e;
            foreach (var candidate in e.Candidates)
            {
                switch (candidate.Outcome)
                {
                    case SceneCandidateOutcome.ConditionFalse:
                        Add(StudioLogCategory.Skip, new StudioText("studio/log_text_condition", candidate.Id), true);
                        break;
                    case SceneCandidateOutcome.Completed:
                        Add(StudioLogCategory.Skip, new StudioText("studio/log_text_completed", candidate.Id), true);
                        break;
                    case SceneCandidateOutcome.Shadowed:
                        Add(StudioLogCategory.Skip, new StudioText("studio/log_text_shadowed", candidate.Id, candidate.ModId), true);
                        break;
                    case SceneCandidateOutcome.Failed:
                        Add(StudioLogCategory.Skip, new StudioText("studio/log_text_failed", candidate.Id), true);
                        break;
                }
            }
        }

        public void OnDialogEnded(DialogEndedEvent e)
        {
            if (!string.IsNullOrEmpty(e.LockerId))
                Add(StudioLogCategory.End, new StudioText("studio/log_text_ended", e.LockerId));
        }

        private string Now()
        {
            var seconds = (Time.GetTicksMsec() - _startMsec) / 1000;
            return $"{seconds / 60:00}:{seconds % 60:00}";
        }
    }
}
