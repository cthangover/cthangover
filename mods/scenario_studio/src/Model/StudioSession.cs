namespace Cthangover.Mods.ScenarioStudio
{
    using System.Collections.Generic;

    /// <summary>
    /// Everything the dock shows at one moment, built by
    /// <see cref="StudioSessionBuilder"/> from the live game: where the player is
    /// (scene, scenario, label), the run state, which mod owns the scenario and
    /// where edits go, the scenario lines (with pending edits) and debugger
    /// marks, the scene candidates, game state, the log and the scene map. A
    /// plain view model: pages render it and never change the game through it.
    /// </summary>
    public sealed class StudioSession
    {
        public string SceneName { get; set; }
        public string ScenarioName { get; set; }

        /// <summary>Label the current line belongs to; null before the first label.</summary>
        public string LabelName { get; set; }

        public StudioRunState RunState { get; set; }

        /// <summary>Mod (or layer) that owns the open scenario file; null without a scenario.</summary>
        public StudioModScope FileScope { get; set; }

        /// <summary>Where edits are written: the file's own folder mod, or the studio layer.</summary>
        public StudioModScope EditScope { get; set; }

        /// <summary>Unapplied edits of the open scenario.</summary>
        public int PendingCount { get; set; }

        public List<StudioCodeLine> Lines { get; } = new();
        public List<StudioCandidate> Candidates { get; } = new();
        public List<StudioStateEntry> State { get; } = new();
        public List<StudioLogEntry> Log { get; } = new();
        public List<StudioSceneNode> Nodes { get; } = new();

        public bool HasScenario => !string.IsNullOrEmpty(ScenarioName);

        /// <summary>Index of the line the dialog stands on, or -1.</summary>
        public int CurrentIndex => Lines.FindIndex(IsCurrentLine);

        /// <summary>
        /// Line index (into the scenario text, 0-based) where "insert after the
        /// current line" puts a line: right after it, or after the whole answer
        /// list of a question — a line between <c>select</c> and its options would
        /// cut the question short in the parser. Without a current line: the end.
        /// </summary>
        public int InsertionTextIndexAfterCurrent()
        {
            var index = CurrentIndex;
            if (index < 0)
                return Lines.Count == 0 ? 0 : Lines[^1].Number;

            var at = index;
            if (Lines[index].Kind == StudioLineKind.Choice)
            {
                while (at + 1 < Lines.Count && Lines[at + 1].Kind == StudioLineKind.Option)
                    at++;
            }
            return Lines[at].Number;
        }

        private static bool IsCurrentLine(StudioCodeLine line) => line.IsCurrent;
    }
}
