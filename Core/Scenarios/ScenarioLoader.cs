using Cthangover.Core.Localization;
using Cthangover.Core.UI.Dialog;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Scenarios
{
    public static class ScenarioLoader
    {
        public static DialogQueue Load(string path)
        {
            return Load(path, GodotTranslationProvider.Instance);
        }

        public static DialogQueue Load(string path, ILocalizationProvider provider)
        {
            var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
            if (file == null)
            {
                GameLogger.Log("SCENE", $"ScenarioLoader: failed to open '{path}'", LogLevel.Error);
                return null;
            }

            var text = file.GetAsText();
            file.Close();

            return ScenarioParser.Parse(text, provider);
        }
    }
}
