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
    /// answer, which starts broken until its target is chosen. Requests carry
    /// 0-based indices into the scenario text, where the dock inserts. Ends
    /// with the same "this change affects" plate as the author mode.
    /// </summary>
    public sealed class StudioNovicePage
    {
        /// <summary>A new line is requested at the given text index.</summary>
        public event Action<int> LineRequested;

        /// <summary>A new answer is requested at the given text index.</summary>
        public event Action<int> OptionRequested;

        /// <summary>"Edit" on a line, question or answer.</summary>
        public event Action<StudioActionButton> EditRequested;

        /// <summary>"Choose where" on an answer.</summary>
        public event Action<StudioActionButton> TargetRequested;

        private StudioMoment _moment;

        public void Build(VBoxContainer content, StudioSession session)
        {
            _moment = StudioMoment.Build(session);
            if (_moment.Say == null)
            {
                content.AddChild(UiKit.EmptyState(TranslationServer.Translate(session.HasScenario ? "studio/moment_empty" : "studio/no_scenario"), UiTokens.FontBody));
                return;
            }

            content.AddChild(StudioStyles.Caption(TranslationServer.Translate("studio/moment_now")));
            foreach (var line in _moment.Before)
                content.AddChild(new StudioSayView(line, true, OnEdit));
            content.AddChild(new StudioSayView(_moment.Say, false, OnEdit));

            if (_moment.IsQuestion)
            {
                content.AddChild(StudioStyles.Caption(TranslationServer.Translate("studio/moment_answers")));
                foreach (var branch in _moment.Branches)
                    content.AddChild(new StudioBranchView(branch, OnEdit, OnTarget));
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
                if (candidate.Status == StudioCandidateStatus.Queued)
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
            var say = _moment.Say.Number - 1;
            LineRequested?.Invoke(_moment.IsQuestion ? say : say + 1);
        }

        private void OnOptionPressed()
        {
            var last = _moment.Branches.Count > 0 ? _moment.Branches[^1].Line : _moment.Say;
            OptionRequested?.Invoke(last.Number);
        }

        private void OnEdit(StudioActionButton button) => EditRequested?.Invoke(button);

        private void OnTarget(StudioActionButton button) => TargetRequested?.Invoke(button);
    }
}
