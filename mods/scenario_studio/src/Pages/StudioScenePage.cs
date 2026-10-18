namespace Cthangover.Mods.ScenarioStudio
{
    using System;
    using Cthangover.Core.UI.Theming;
    using Cthangover.Mods.Interface.Theming;
    using Godot;

    /// <summary>
    /// "Scene" tab: every scenario that targets the current scene, in start
    /// order, with the owning mod, priority, header condition and its result,
    /// and the final status with the reason in words. This is the answer to
    /// "why did my scenario not start": a false condition, a one-run scenario
    /// already completed, or another mod winning the same scenario name.
    /// </summary>
    public static class StudioScenePage
    {
        public static void Build(VBoxContainer content, StudioSession session, Action<string> picked)
        {
            var hint = UiKit.Hint(TranslationServer.Translate("studio/cand_hint"));
            hint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            content.AddChild(hint);

            foreach (var candidate in session.Candidates)
                content.AddChild(BuildCard(candidate, picked));
        }

        private static Control BuildCard(StudioCandidate candidate, Action<string> picked)
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
            var condition = (candidate.ConditionOk ? "✓ " : "✗ ") + (candidate.Condition ?? TranslationServer.Translate("studio/no_condition"));
            meta.AddChild(StudioStyles.Text(condition, UiTokens.FontSmall, candidate.ConditionOk ? UiTokens.SuccessText : UiTokens.DangerText, mono: true));
            if (candidate.IsOneRun)
                meta.AddChild(StudioStyles.Text(TranslationServer.Translate("studio/one_run"), UiTokens.FontSmall, UiTokens.Muted));
            column.AddChild(meta);

            var reason = UiKit.Label(candidate.Reason.Resolve(), UiTokens.FontSmall, UiTokens.Muted);
            reason.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            column.AddChild(reason);

            if (running)
                return card;

            var actions = UiKit.Row(UiTokens.Space2);
            actions.AddChild(StudioPrototypeButton.Create("studio/run_now", picked));
            if (candidate.Status == StudioCandidateStatus.Completed)
                actions.AddChild(StudioPrototypeButton.Create("studio/reset_completed", picked));
            column.AddChild(actions);
            return card;
        }

        private static string StatusKey(StudioCandidateStatus status)
        {
            switch (status)
            {
                case StudioCandidateStatus.Running:
                    return "studio/cand_running";
                case StudioCandidateStatus.Waiting:
                    return "studio/cand_waiting";
                case StudioCandidateStatus.Skipped:
                    return "studio/cand_skipped";
                case StudioCandidateStatus.Completed:
                    return "studio/cand_completed";
                default:
                    return "studio/cand_shadowed";
            }
        }
    }
}
