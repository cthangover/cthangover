namespace Cthangover.Mods.ScenarioStudio
{
    using System;
    using Cthangover.Core.UI.Theming;
    using Cthangover.Mods.Interface.Theming;
    using Godot;

    /// <summary>
    /// One answer of the current question for the novice (<c>.ct-branch</c>):
    /// the answer text and, under it, where it leads — a scene, an action, the
    /// end of the scenario — with the label hop already followed. Every answer
    /// can be redirected ("Choose where"); a broken one says so in words and
    /// offers only that, as the gold action.
    /// </summary>
    public partial class StudioBranchView : PanelContainer
    {
        public bool IsBroken { get; }

        public StudioBranchView(StudioBranch branch, Action<StudioActionButton> edit, Action<StudioActionButton> chooseTarget)
        {
            IsBroken = branch.Kind == StudioOutcomeKind.Broken;
            var isNew = branch.Line.IsNew;
            MouseFilter = MouseFilterEnum.Ignore;
            AddThemeStyleboxOverride("panel", StudioStyles.Branch(IsBroken, isNew));

            var row = UiKit.Row(StudioStyles.BarGap);
            AddChild(row);
            if (isNew)
                row.AddChild(StudioStyles.Bar(UiTokens.Success));

            var body = UiKit.Column(UiTokens.Space1);
            body.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            row.AddChild(body);

            var text = UiKit.Label(StudioSayView.Quote(branch.Line), UiTokens.FontBodyLarge, UiTokens.Ink);
            text.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            body.AddChild(text);
            body.AddChild(BuildOutcome(branch));

            var actions = UiKit.Column(UiTokens.Space1);
            actions.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            row.AddChild(actions);

            var target = StudioActionButton.Create("studio/choose_target", chooseTarget, primary: IsBroken);
            target.Line = branch.Line;
            actions.AddChild(target);
            if (IsBroken)
                return;

            var editText = StudioActionButton.Create("studio/edit", edit);
            editText.Line = branch.Line;
            actions.AddChild(editText);
        }

        private static Control BuildOutcome(StudioBranch branch)
        {
            var color = StudioStyles.OutcomeColor(branch.Kind);
            var row = UiKit.Row(UiTokens.Space2);
            row.AddChild(StudioStyles.Text("→", UiTokens.FontBodySmall, UiTokens.Faint));
            row.AddChild(StudioStyles.Text(TranslationServer.Translate(OutcomeKey(branch.Kind)), UiTokens.FontBodySmall, color));
            if (!string.IsNullOrEmpty(branch.Target))
                row.AddChild(StudioStyles.Text(branch.Target, UiTokens.FontBodySmall, color, mono: true));
            return row;
        }

        private static string OutcomeKey(StudioOutcomeKind kind)
        {
            switch (kind)
            {
                case StudioOutcomeKind.Scene:
                    return "studio/outcome_scene";
                case StudioOutcomeKind.Action:
                    return "studio/outcome_action";
                case StudioOutcomeKind.End:
                    return "studio/outcome_end";
                case StudioOutcomeKind.Continue:
                    return "studio/outcome_continue";
                default:
                    return "studio/outcome_broken";
            }
        }
    }
}
