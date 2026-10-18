namespace Cthangover.Mods.ScenarioStudio
{
    using System;
    using Godot;

    /// <summary>
    /// Button of an action the prototype shows but does not perform yet (run a
    /// candidate, go to a scene, edit a state value…). It reports its own
    /// caption, so one handler in the dock can say which action is not wired
    /// without a closure per button.
    /// </summary>
    public partial class StudioPrototypeButton : Button
    {
        /// <summary>Raised with the button caption when pressed.</summary>
        public event Action<string> Picked;

        public StudioPrototypeButton()
        {
            Pressed += OnPressed;
        }

        /// <summary>Small studio button with a localized caption, reporting to <paramref name="picked"/>.</summary>
        public static StudioPrototypeButton Create(string key, Action<string> picked)
        {
            var button = new StudioPrototypeButton();
            StudioStyles.SmallButton(button, TranslationServer.Translate(key));
            button.Picked += picked;
            return button;
        }

        private void OnPressed() => Picked?.Invoke(Text);
    }
}
