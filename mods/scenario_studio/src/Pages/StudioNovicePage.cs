namespace Cthangover.Mods.ScenarioStudio
{
    using System;
    using Cthangover.Core.UI.Theming;
    using Cthangover.Mods.Interface.Theming;
    using Godot;

    /// <summary>
    /// "Now" tab in the novice mode: the moment of the story rather than the
    /// script (<see cref="StudioMoment"/>) — what is on screen, the lines said
    /// right before it, every answer with where it leads, then what else the
    /// scene has queued. The only insert actions are the ones that make sense
    /// here: a line before the question (or after a plain line) and a new
    /// answer, which starts broken until its target is chosen. Ends with the
    /// same "this change affects" plate as the author mode.
    /// </summary>
    public sealed class StudioNovicePage
    {
        /// <summary>A new line is requested at the given line index.</summary>
        public event Action<int> LineRequested;

        /// <summary>A new answer is requested at the given line index.</summary>
        public event Action<int> OptionRequested;

        private StudioMoment _moment;

        public void Build(VBoxContainer content, StudioSession session, Action<string> picked)
        {
            _moment = StudioMoment.Build(session);
            if (_moment.Say == null)
            {
                content.AddChild(UiKit.EmptyState(TranslationServer.Translate("studio/moment_empty"), UiTokens.FontBody));
                return;
            }

            content.AddChild(StudioStyles.Caption(TranslationServer.Translate("studio/moment_now")));
            foreach (var line in _moment.Before)
                content.AddChild(new StudioSayView(line, true, picked));
            content.AddChild(new StudioSayView(_moment.Say, false, picked));

            if (_moment.IsQuestion)
            {
                content.AddChild(StudioStyles.Caption(TranslationServer.Translate("studio/moment_answers")));
                foreach (var branch in _moment.Branches)
                    content.AddChild(new StudioBranchView(branch, picked));
            }

            var actions = UiKit.Row(UiTokens.Space2);
            actions.AddChild(InsertButton(_moment.IsQuestion ? "studio/add_line_before" : "studio/add_line_after", OnLinePressed));
            if (_moment.IsQuestion)
                actions.AddChild(InsertButton("studio/add_option", OnOptionPressed));
            content.AddChild(actions);

            content.AddChild(StudioStyles.Caption(TranslationServer.Translate("studio/moment_next")));
            content.AddChild(BuildNext(session));
            content.AddChild(StudioNowPage.BuildScopeBanner(session));
        }

        private static Control BuildNext(StudioSession session)
        {
            foreach (var candidate in session.Candidates)
            {
                if (candidate.Status == StudioCandidateStatus.Waiting)
                    return UiKit.Hint(string.Format(TranslationServer.Translate("studio/next_waiting"), candidate.Name), UiTokens.FontBodySmall);
            }
            return UiKit.Hint(TranslationServer.Translate("studio/next_none"), UiTokens.FontBodySmall);
        }

        private static Button InsertButton(string key, Action handler)
        {
            var button = StudioStyles.SmallButton(new Button(), TranslationServer.Translate(key));
            button.Pressed += handler;
            return button;
        }

        private void OnLinePressed()
        {
            var at = _moment.IsQuestion ? _moment.SayIndex : _moment.SayIndex + 1;
            LineRequested?.Invoke(at);
        }

        private void OnOptionPressed() => OptionRequested?.Invoke(_moment.AfterBranchesIndex);
    }
}
