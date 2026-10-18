namespace Cthangover.Mods.ScenarioStudio
{
    using System;
    using Godot;

    /// <summary>
    /// Small studio button that carries what it acts on — a scenario line, a
    /// scenario or scene name — and reports itself when pressed, so one dock
    /// handler serves every row without a closure per button.
    /// </summary>
    public partial class StudioActionButton : Button
    {
        /// <summary>Name the action applies to (scenario, scene), if any.</summary>
        public string Payload { get; set; }

        /// <summary>Scenario line the action applies to, if any.</summary>
        public StudioCodeLine Line { get; set; }

        /// <summary>Raised with the button itself when pressed.</summary>
        public event Action<StudioActionButton> Chosen;

        public StudioActionButton()
        {
            Pressed += OnPressed;
        }

        /// <summary>Small studio-styled button with a localized caption.</summary>
        public static StudioActionButton Create(string key, Action<StudioActionButton> chosen, bool primary = false)
        {
            var button = new StudioActionButton();
            StudioStyles.SmallButton(button, TranslationServer.Translate(key), primary);
            button.Chosen += chosen;
            return button;
        }

        private void OnPressed() => Chosen?.Invoke(this);
    }
}
