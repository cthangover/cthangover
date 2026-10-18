namespace Cthangover.Mods.ScenarioStudio
{
    using System;
    using Godot;

    /// <summary>
    /// Hands the text field of a <see cref="StudioPrompt.AskText"/> dialog to its
    /// callback on OK. A node child of the dialog, so it lives and dies with it
    /// and the dialog needs no closure over the field.
    /// </summary>
    public partial class StudioPromptRelay : Node
    {
        private readonly LineEdit _field;
        private readonly Action<string> _done;

        public StudioPromptRelay(LineEdit field, Action<string> done)
        {
            _field = field;
            _done = done;
        }

        public void OnConfirmed() => _done?.Invoke(_field.Text);
    }
}
