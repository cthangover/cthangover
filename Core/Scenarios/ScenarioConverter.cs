using System;
using System.Collections.Generic;
using Cthangover.Core.Localization;
using Cthangover.Core.UI.Dialog;
using Cthangover.Core.UI.Dialog.Action;
using Cthangover.Core.UI.Dialog.Action.Impls;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Scenarios
{
    public class ScenarioConverter
    {
        private readonly ILocalizationProvider locale;

        public ScenarioConverter(ILocalizationProvider locale = null)
        {
            this.locale = locale ?? GodotTranslationProvider.Instance;
        }

        public DialogQueue Convert(string text)
        {
            return ScenarioParser.Parse(text, locale);
        }

        public DialogQueue ConvertFile(string path)
        {
            var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
            if (file == null)
            {
                GameLogger.Log("CONVERTER", $"Failed to open '{path}'", LogLevel.Error);
                return null;
            }

            var text = file.GetAsText();
            file.Close();

            return Convert(text);
        }
    }
}
