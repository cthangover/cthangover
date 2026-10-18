namespace Cthangover.Mods.ScenarioStudio
{
    using System.Collections.Generic;

    /// <summary>
    /// Everything the studio dock shows about the running game: where the player
    /// is (scene, scenario, label), which mod owns the open scenario and where
    /// edits will be written, the scenario lines with the debugger position, the
    /// scene candidates, game state, the event log and the scene map.
    /// <para/>
    /// Prototype stage: <see cref="CreateStub"/> fills it with a hand-written
    /// picture of <c>home_kitchen</c> (the real <c>home_kitchen_cooking</c>
    /// scenario of the cooking mod), and the debugger operations only move the
    /// markers so the layout can be judged in motion. The runtime session that
    /// replaces the stub reads the same shape from the dialog runtime and the
    /// scene composition.
    /// </summary>
    public sealed class StudioSession
    {
        public string SceneName { get; set; }
        public string ScenarioName { get; set; }
        /// <summary>Label the current line belongs to; null before the first label.</summary>
        public string LabelName { get; set; }
        public bool IsPaused { get; set; }

        /// <summary>Mod that owns the open scenario file.</summary>
        public StudioModScope FileScope { get; set; }

        /// <summary>Where edits are written: the file's own mod for a folder mod, the studio layer otherwise.</summary>
        public StudioModScope EditScope { get; set; }

        public List<StudioCodeLine> Lines { get; } = new();
        public List<StudioCandidate> Candidates { get; } = new();
        public List<StudioStateEntry> State { get; } = new();
        public List<StudioLogEntry> Log { get; } = new();
        public List<StudioSceneNode> Nodes { get; } = new();

        /// <summary>Index of the line the dialog stands on, or -1 when nothing runs.</summary>
        public int CurrentIndex => Lines.FindIndex(IsCurrentLine);

        /// <summary>Lines added in the studio and not applied to the running scenario yet.</summary>
        public int PendingCount => Lines.FindAll(IsNewLine).Count;

        /// <summary>Moves the current marker to the next line, marking the left one as executed.</summary>
        public void Step()
        {
            var index = CurrentIndex;
            if (index < 0 || index + 1 >= Lines.Count)
                return;

            Lines[index].IsCurrent = false;
            Lines[index].IsDone = true;
            Lines[index + 1].IsCurrent = true;
            IsPaused = true;
        }

        /// <summary>
        /// Where "insert after the current line" puts a line: right after it, or
        /// after the whole answer list when the current line is a question —
        /// a line between <c>select</c> and its <c>option</c>s would cut the
        /// question short in the parser.
        /// </summary>
        public int InsertionIndexAfterCurrent()
        {
            var index = CurrentIndex;
            if (index < 0)
                return Lines.Count;

            var at = index + 1;
            if (Lines[index].Kind == StudioLineKind.Choice)
            {
                while (at < Lines.Count && Lines[at].Kind == StudioLineKind.Option)
                    at++;
            }
            return at;
        }

        /// <summary>
        /// Inserts a line at <paramref name="at"/> and renumbers the tail. The line
        /// stays marked new until <see cref="Apply"/>, which is how the dock shows
        /// unapplied edits.
        /// </summary>
        public void InsertAt(int at, StudioCodeLine line)
        {
            at = System.Math.Clamp(at, 0, Lines.Count);
            line.Number = at < Lines.Count ? Lines[at].Number : Lines.Count == 0 ? 1 : Lines[^1].Number + 1;
            line.IsNew = true;
            Lines.Insert(at, line);

            for (var i = at + 1; i < Lines.Count; i++)
                Lines[i].Number++;
        }

        /// <summary>Accepts the pending lines and logs the edit against the edit scope.</summary>
        public void Apply(string time)
        {
            var count = PendingCount;
            if (count == 0)
                return;

            foreach (var line in Lines)
                line.IsNew = false;

            Log.Add(new StudioLogEntry
            {
                Time = time,
                Category = StudioLogCategory.Edit,
                Text = new StudioText("studio/log_text_applied", ScenarioName, count, EditScope.ModId),
            });
        }

        private static bool IsCurrentLine(StudioCodeLine line) => line.IsCurrent;

        private static bool IsNewLine(StudioCodeLine line) => line.IsNew;

        /// <summary>
        /// Hand-written picture of the kitchen used until the dock is wired to the
        /// runtime. Only data lives here — ids, numbers, DSL in its key-only form;
        /// every phrase is a localization key resolved when shown.
        /// </summary>
        public static StudioSession CreateStub()
        {
            var cooking = new StudioModScope("cooking", StudioScopeKind.Overlay);
            var core = new StudioModScope("visual_novel_core", StudioScopeKind.Overlay);
            var layer = new StudioModScope("my_studio", StudioScopeKind.Layer);

            var session = new StudioSession
            {
                SceneName = "home_kitchen",
                ScenarioName = "home_kitchen_cooking",
                IsPaused = true,
                FileScope = cooking,
                EditScope = layer,
            };

            AddLine(session, 4, StudioLineKind.Light, "light_set [{\"x\":0.43,\"y\":0.57,\"radius\":500},{\"x\":0.10,\"y\":0.27,\"radius\":500}]", null, null).IsDone = true;
            var current = AddLine(session, 5, StudioLineKind.Choice, "select key=rooms/kitchen/prompt", new StudioText("rooms/kitchen/prompt"), null);
            current.IsCurrent = true;
            current.HasBreakpoint = true;
            AddLine(session, 6, StudioLineKind.Option, "option key=rooms/kitchen/opt_exit -> :exit", new StudioText("rooms/kitchen/opt_exit"), "exit");
            AddLine(session, 7, StudioLineKind.Option, "option key=rooms/kitchen/opt_cook -> :cook", new StudioText("rooms/kitchen/opt_cook"), "cook");
            AddLine(session, 8, StudioLineKind.Jump, "goto :yard", null, "yard").Error = new StudioText("studio/error_label_missing", ":yard");
            AddLine(session, 10, StudioLineKind.Label, ":exit", null, "exit");
            AddLine(session, 11, StudioLineKind.Jump, "switch_scene home_vestibule", null, "home_vestibule");
            AddLine(session, 12, StudioLineKind.End, "end", null, null);
            AddLine(session, 14, StudioLineKind.Label, ":cook", null, "cook");
            AddLine(session, 15, StudioLineKind.Action, "action toggle_cooking_workbench", null, "toggle_cooking_workbench");
            AddLine(session, 16, StudioLineKind.End, "end", null, null);

            session.Candidates.Add(new StudioCandidate { Name = "home_kitchen_cooking", Scope = cooking, Priority = 5, ConditionOk = true, Status = StudioCandidateStatus.Running, Reason = new StudioText("studio/reason_order", 5) });
            session.Candidates.Add(new StudioCandidate { Name = "home_kitchen_default", Scope = core, Priority = 5, ConditionOk = true, Status = StudioCandidateStatus.Waiting, Reason = new StudioText("studio/reason_waiting", "home_kitchen_cooking") });
            session.Candidates.Add(new StudioCandidate { Name = "kitchen_evening_tea", Scope = layer, Priority = 20, Condition = "time.hour >= 18", ConditionOk = false, Status = StudioCandidateStatus.Skipped, Reason = new StudioText("studio/reason_condition_false", "09:40") });
            session.Candidates.Add(new StudioCandidate { Name = "kitchen_first_visit", Scope = core, Priority = 1, ConditionOk = true, IsOneRun = true, Status = StudioCandidateStatus.Completed, Reason = new StudioText("studio/reason_completed") });

            AddState(session, "studio/group_time", new StudioText("studio/state_day"), StudioText.Raw("3"));
            AddState(session, "studio/group_time", new StudioText("studio/state_hour"), StudioText.Raw("09:40"));
            AddState(session, "studio/group_quests", StudioText.Raw("murakami_intro"), new StudioText("studio/quest_stage", 2, new StudioText("studio/quest_in_progress")));
            AddState(session, "studio/group_quests", StudioText.Raw("cooking_first_meal"), new StudioText("studio/quest_not_started"));
            AddState(session, "studio/group_inventory", StudioText.Raw("ration"), StudioText.Raw("4"));
            AddState(session, "studio/group_inventory", StudioText.Raw("branches"), StudioText.Raw("10"));
            AddState(session, "studio/group_dialog_vars", StudioText.Raw("${choice}"), new StudioText("studio/value_none"));
            AddState(session, "studio/group_completed", StudioText.Raw("prolog_alley_intro"), new StudioText("studio/value_yes"));
            AddState(session, "studio/group_completed", StudioText.Raw("kitchen_first_visit"), new StudioText("studio/value_yes"));

            AddLog(session, "00:41", StudioLogCategory.Choice, new StudioText("studio/log_text_choice", "home_vestibule", "home_kitchen"));
            AddLog(session, "00:42", StudioLogCategory.Scene, new StudioText("studio/log_text_enter", "home_kitchen"));
            AddLog(session, "00:42", StudioLogCategory.Skip, new StudioText("studio/log_text_condition", "kitchen_evening_tea")).IsWarning = true;
            AddLog(session, "00:42", StudioLogCategory.Skip, new StudioText("studio/log_text_completed", "kitchen_first_visit")).IsWarning = true;
            AddLog(session, "00:43", StudioLogCategory.Scenario, new StudioText("studio/log_text_started", "home_kitchen_cooking", 4));
            AddLog(session, "00:43", StudioLogCategory.Stop, new StudioText("studio/log_text_breakpoint", 5));

            session.Nodes.Add(new StudioSceneNode { Name = "prolog_alley", State = StudioNodeState.Visited, Scope = core });
            session.Nodes.Add(new StudioSceneNode { Name = "home_vestibule", State = StudioNodeState.Visited, Scope = core, Via = new StudioText("studio/via_option", ":kitchen") });
            session.Nodes.Add(new StudioSceneNode { Name = "home_kitchen", State = StudioNodeState.Here, Scope = core });
            session.Nodes.Add(new StudioSceneNode { Name = "home_vestibule", State = StudioNodeState.Next, Scope = core, Via = new StudioText("studio/via_switch", ":exit") });
            session.Nodes.Add(new StudioSceneNode { Name = "yard", State = StudioNodeState.New, Scope = layer, Via = new StudioText("studio/via_goto", ":yard") });

            return session;
        }

        private static StudioCodeLine AddLine(StudioSession session, int number, StudioLineKind kind, string text, StudioText say, string target)
        {
            var line = new StudioCodeLine { Number = number, Kind = kind, Text = text, Say = say, Target = target };
            session.Lines.Add(line);
            return line;
        }

        private static void AddState(StudioSession session, string groupKey, StudioText key, StudioText value)
        {
            session.State.Add(new StudioStateEntry { Group = new StudioText(groupKey), Key = key, Value = value });
        }

        private static StudioLogEntry AddLog(StudioSession session, string time, StudioLogCategory category, StudioText text)
        {
            var entry = new StudioLogEntry { Time = time, Category = category, Text = text };
            session.Log.Add(entry);
            return entry;
        }
    }
}
