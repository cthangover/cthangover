namespace Cthangover.Mods.ScenarioStudio
{
    using System.Collections.Generic;
    using Cthangover.Core.Bootstrap;
    using Cthangover.Core.Mods.Resolvers;
    using Cthangover.Core.Quests;
    using Cthangover.Core.Scenes;
    using Cthangover.Core.Settings;
    using Cthangover.Mods.Dialog.Executable;
    using Godot;

    /// <summary>
    /// Builds the <see cref="StudioSession"/> the dock renders from the live
    /// game: the current scenario's lines (pending edits included) with the
    /// debugger's marks, its file and edit scopes, the scene candidates of the
    /// last composition merged with what is true now (running, completed), the
    /// game state worth seeing while debugging, the log and the scene map
    /// around the player. Read-only: nothing here changes the game.
    /// </summary>
    public static class StudioSessionBuilder
    {
        private const int VisitedOnMap = 4;

        public static StudioSession Build(StudioLive live)
        {
            var session = new StudioSession { SceneName = StudioLive.SceneName };
            var evt = live.CurrentEvent;
            session.RunState = RunState(live);

            if (evt != null)
                FillScenario(session, live, evt);

            FillCandidates(session, live, evt);
            FillState(session, live);
            session.Log.AddRange(live.Journal.Entries);
            FillMap(session, live);
            return session;
        }

        private static StudioRunState RunState(StudioLive live)
        {
            var box = live.Box;
            if (box != null && box.IsRunning && box.Runtime.IsHeld)
                return StudioRunState.Held;
            if (live.Debugger.IsPaused)
                return StudioRunState.Paused;
            return live.IsCurrentRunning ? StudioRunState.Running : StudioRunState.Idle;
        }

        private static void FillScenario(StudioSession session, StudioLive live, ScenarioEvent evt)
        {
            session.ScenarioName = evt.ID;
            session.FileScope = live.ScopeOf(evt.SourceModId, evt.SourcePath);
            session.EditScope = live.EditScopeFor(session.FileScope);

            var copy = live.Copy(evt.ID);
            session.PendingCount = copy?.PendingCount ?? 0;
            var lines = StudioLineReader.Read(copy != null ? copy.Text : StudioLive.SourceText(evt));

            var running = live.IsCurrentRunning && live.Debugger.Event == evt;
            var current = live.IsCurrentRunning ? live.CurrentLine() : 0;
            string label = null;
            foreach (var line in lines)
            {
                line.SourceLine = copy != null ? copy.OriginalNumber(line.Number) : line.Number;
                line.IsNew = copy != null && copy.IsChanged(line.Number - 1);
                line.HasBreakpoint = line.SourceLine > 0 && live.Debugger.HasBreakpoint(evt.ID, line.SourceLine);
                line.IsCurrent = current > 0 && line.SourceLine == current;
                line.IsDone = running && !line.IsCurrent && line.SourceLine > 0 && live.Debugger.IsExecuted(line.SourceLine);
                line.CanRunNext = running && !line.IsCurrent && live.CanRunFrom(line.SourceLine);
                if (line.Kind == StudioLineKind.Label && current > 0 && line.SourceLine > 0 && line.SourceLine < current)
                    label = ":" + line.Target;
            }
            session.LabelName = label;
            session.Lines.AddRange(lines);
        }

        private static void FillCandidates(StudioSession session, StudioLive live, ScenarioEvent evt)
        {
            var report = live.Journal.Composed(session.SceneName);
            if (report == null)
                return;

            var completed = GameData.Instance?.Runtime.CompletedScenarioIds;
            foreach (var candidate in report.Candidates)
            {
                var item = new StudioCandidate
                {
                    Name = candidate.Id,
                    Scope = live.ScopeOf(candidate.ModId, candidate.ScenarioPath),
                    Priority = candidate.Priority,
                    Condition = string.IsNullOrEmpty(candidate.Condition) ? null : candidate.Condition,
                    IsOneRun = candidate.IsOneRun,
                };

                switch (candidate.Outcome)
                {
                    case SceneCandidateOutcome.ConditionFalse:
                        item.ConditionOk = false;
                        item.Status = StudioCandidateStatus.Skipped;
                        item.Reason = new StudioText("studio/reason_condition_false");
                        break;
                    case SceneCandidateOutcome.Completed:
                        item.Status = StudioCandidateStatus.Completed;
                        item.Reason = new StudioText("studio/reason_completed");
                        break;
                    case SceneCandidateOutcome.Shadowed:
                        item.Status = StudioCandidateStatus.Shadowed;
                        item.Reason = new StudioText("studio/reason_shadowed");
                        break;
                    case SceneCandidateOutcome.Failed:
                        item.Status = StudioCandidateStatus.Failed;
                        item.Reason = new StudioText("studio/reason_failed");
                        break;
                    default:
                        item.ConditionOk = true;
                        item.Status = StudioCandidateStatus.Queued;
                        item.Reason = new StudioText("studio/reason_queued", candidate.Priority);
                        break;
                }

                if (evt != null && live.IsCurrentRunning && evt.ID == candidate.Id)
                {
                    item.Status = StudioCandidateStatus.Running;
                    item.Reason = new StudioText("studio/reason_running");
                }
                else if (completed != null && candidate.IsOneRun && completed.Contains(candidate.Id))
                {
                    item.Status = StudioCandidateStatus.Completed;
                    item.Reason = new StudioText("studio/reason_completed");
                }

                session.Candidates.Add(item);
            }
        }

        private static void FillState(StudioSession session, StudioLive live)
        {
            var runtime = GameData.Instance?.Runtime;
            if (runtime == null)
                return;

            var time = runtime.Time;
            Add(session, "studio/group_time", new StudioText("studio/state_day"), StudioText.Raw(time.Days.ToString()), StudioStateEditor.None, null);
            Add(session, "studio/group_time", new StudioText("studio/state_hour"), StudioText.Raw($"{time.Hours:00}:{time.Minutes:00}"), StudioStateEditor.None, null);

            var box = live.Box;
            if (box != null && box.IsRunning)
            {
                foreach (var variable in box.Runtime.Variables)
                    Add(session, "studio/group_dialog_vars", StudioText.Raw("${" + variable.Key + "}"), StudioText.Raw(variable.Value), StudioStateEditor.DialogVariable, variable.Key);
            }

            var quests = QuestServiceRegistry.Provider;
            if (quests != null)
            {
                foreach (var id in QuestIds(session.Lines))
                {
                    var quest = quests.Get(id);
                    if (quest != null)
                        Add(session, "studio/group_quests", StudioText.Raw(id), StudioText.Raw(quest.Status.ToString()), StudioStateEditor.Quest, id);
                }
            }

            var inventory = runtime.Inventory;
            if (inventory?.Items != null)
            {
                foreach (var slot in inventory.Items)
                {
                    if (slot?.Item != null)
                        Add(session, "studio/group_inventory", StudioText.Raw(slot.Item.ID), StudioText.Raw(slot.Count.ToString()), StudioStateEditor.Inventory, slot.Item.ID);
                }
            }

            foreach (var id in runtime.CompletedScenarioIds)
                Add(session, "studio/group_completed", StudioText.Raw(id), new StudioText("studio/value_yes"), StudioStateEditor.None, id);
        }

        private static IEnumerable<string> QuestIds(List<StudioCodeLine> lines)
        {
            var seen = new HashSet<string>();
            foreach (var line in lines)
            {
                var at = line.Text.IndexOf("quest_id=", System.StringComparison.Ordinal);
                if (at < 0)
                    continue;
                var start = at + "quest_id=".Length;
                var end = line.Text.IndexOf(' ', start);
                var id = (end < 0 ? line.Text.Substring(start) : line.Text.Substring(start, end - start)).Trim('"');
                if (id.Length > 0 && seen.Add(id))
                    yield return id;
            }
        }

        private static void Add(StudioSession session, string group, StudioText key, StudioText value, StudioStateEditor editor, string id)
        {
            session.State.Add(new StudioStateEntry { Group = new StudioText(group), Key = key, Value = value, Editor = editor, Id = id });
        }

        private static void FillMap(StudioSession session, StudioLive live)
        {
            var here = session.SceneName;
            var scenes = AppServices.Current.SceneDefs.CollectAll();

            var visited = new List<string>();
            var history = live.Journal.SceneHistory;
            for (var i = history.Count - 1; i >= 0 && visited.Count < VisitedOnMap; i--)
            {
                if (history[i] != here && !visited.Contains(history[i]))
                    visited.Insert(0, history[i]);
            }
            foreach (var scene in visited)
                session.Nodes.Add(Node(live, scenes, scene, StudioNodeState.Visited, null));

            if (string.IsNullOrEmpty(here))
                return;
            session.Nodes.Add(Node(live, scenes, here, StudioNodeState.Here, null));

            var exits = new HashSet<string>();
            var defs = ScenarioDefs.CollectDefinitions();
            if (defs != null && defs.TryGetValue(here, out var scenarios))
            {
                foreach (var def in scenarios)
                {
                    foreach (var line in StudioLineReader.Read(ScenarioDefs.ReadText(def.ModId, def.FilePath)))
                    {
                        if (line.Kind == StudioLineKind.Jump && !string.IsNullOrEmpty(line.Target) && exits.Add(line.Target))
                            session.Nodes.Add(Node(live, scenes, line.Target, StudioNodeState.Next, new StudioText("studio/via_scenario", def.Name)));
                    }
                }
            }

            foreach (var scene in live.Layer.SceneNames())
            {
                if (scene != here && exits.Add(scene))
                    session.Nodes.Add(Node(live, scenes, scene, StudioNodeState.New, new StudioText("studio/via_layer")));
            }
        }

        private static StudioSceneNode Node(StudioLive live, Dictionary<string, List<SceneDefinition>> scenes, string name, StudioNodeState state, StudioText via)
        {
            string modId = null;
            if (scenes != null && scenes.TryGetValue(name, out var list) && list.Count > 0)
                modId = list[0].ModId;
            var scope = modId == null ? new StudioModScope("?", StudioScopeKind.ReadOnly) : live.ScopeOf(modId, $"scenes/{name}.json");
            return new StudioSceneNode { Name = name, State = state, Scope = scope, Via = via };
        }
    }
}
