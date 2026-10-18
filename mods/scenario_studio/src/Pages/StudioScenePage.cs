namespace Cthangover.Mods.ScenarioStudio
{
    using System;
    using Cthangover.Core.UI.Theming;
    using Cthangover.Mods.Interface.Theming;
    using Godot;

    /// <summary>
    /// "Scene" tab: every scenario the last composition of the current scene
    /// considered, in start order, with the owning mod, priority, header
    /// condition and its result, and the status with the reason in words. This
    /// is the answer to "why did my scenario not start": a false condition, a
    /// one-run scenario already completed, or another mod winning the same
    /// scenario name. Any candidate can be started now; a completed one can be
    /// made fresh again; a new scenario for the scene goes to the studio layer.
    /// </summary>
    public static class StudioScenePage
    {
        public static void Build(VBoxContainer content, StudioSession session, Action<StudioActionButton> run, Action<StudioActionButton> reset, Action<StudioActionButton> create)
        {
            var hint = UiKit.Hint(TranslationServer.Translate("studio/cand_hint"));
            hint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            content.AddChild(hint);

            if (session.Candidates.Count == 0)
                content.AddChild(UiKit.EmptyState(TranslationServer.Translate("studio/cand_none"), UiTokens.FontBody));
            foreach (var candidate in session.Candidates)
                content.AddChild(BuildCard(candidate, run, reset));

            var add = StudioActionButton.Create("studio/new_scenario", create);
            add.Payload = session.SceneName;
            add.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
            content.AddChild(add);
        }

        private static Control BuildCard(StudioCandidate candidate, Action<StudioActionButton> run, Action<StudioActionButton> reset)
        {
            var running = candidate.Status == StudioCandidateStatus.Running;
            var card = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            card.AddThemeStyleboxOverride("panel", StudioStyles.Candidate(running));

            var column = UiKit.Column(UiTokens.Space2);
            card.AddChild(column);

            var head = UiKit.Row(UiTokens.Space3);
            head.AddChild(StudioStyles.Text(candidate.Name, UiTokens.FontBody, UiTokens.InkBright, mono: true));
            head.AddChild(UiKit.Spacer());
            head.AddChild(UiKit.Badge(TranslationServer.Translate(StatusKey(candidate.Status)), StudioStyles.StatusColor(candidate.Status)));
            column.AddChild(head);

            var meta = new HFlowContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            meta.AddThemeConstantOverride("h_separation", UiTokens.Space3);
            meta.AddChild(StudioStyles.Scope(candidate.Scope));
            meta.AddChild(StudioStyles.Text(string.Format(TranslationServer.Translate("studio/priority"), candidate.Priority), UiTokens.FontSmall, UiTokens.Muted, mono: true));
            var condition = candidate.Condition ?? TranslationServer.Translate("studio/no_condition");
            var mark = candidate.ConditionOk == null ? "" : candidate.ConditionOk.Value ? "✓ " : "✗ ";
            var conditionColor = candidate.ConditionOk == false ? UiTokens.DangerText : candidate.ConditionOk == true ? UiTokens.SuccessText : UiTokens.Muted;
            meta.AddChild(StudioStyles.Text(mark + condition, UiTokens.FontSmall, conditionColor, mono: true));
            if (candidate.IsOneRun)
                meta.AddChild(StudioStyles.Text(TranslationServer.Translate("studio/one_run"), UiTokens.FontSmall, UiTokens.Muted));
            column.AddChild(meta);

            var reason = UiKit.Label(candidate.Reason?.Resolve() ?? string.Empty, UiTokens.FontSmall, UiTokens.Muted);
            reason.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            column.AddChild(reason);

            if (running || candidate.Status == StudioCandidateStatus.Shadowed)
                return card;

            var actions = UiKit.Row(UiTokens.Space2);
            var start = StudioActionButton.Create("studio/run_now", run);
            start.Payload = candidate.Name;
            actions.AddChild(start);
            if (candidate.Status == StudioCandidateStatus.Completed)
            {
                var fresh = StudioActionButton.Create("studio/reset_completed", reset);
                fresh.Payload = candidate.Name;
                actions.AddChild(fresh);
            }
            column.AddChild(actions);
            return card;
        }

        private static string StatusKey(StudioCandidateStatus status)
        {
            switch (status)
            {
                case StudioCandidateStatus.Running:
                    return "studio/cand_running";
                case StudioCandidateStatus.Queued:
                    return "studio/cand_queued";
                case StudioCandidateStatus.Skipped:
                    return "studio/cand_skipped";
                case StudioCandidateStatus.Completed:
                    return "studio/cand_completed";
                case StudioCandidateStatus.Failed:
                    return "studio/cand_failed";
                default:
                    return "studio/cand_shadowed";
            }
        }
    }
}
