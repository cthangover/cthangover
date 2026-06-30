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
    /// <summary>
    /// Converts scenario DSL text or files into <see cref="DialogQueue"/> instances.
    /// Wraps <see cref="ScenarioParser"/> with a configurable <see cref="ILocalizationProvider"/>
    /// that defaults to <see cref="GodotTranslationProvider.Instance"/> when none is supplied.
    /// </summary>
    public class ScenarioConverter
    {
        private readonly ILocalizationProvider locale;

        /// <summary>
        /// Creates a converter with an optional localization provider.
        /// </summary>
        /// <param name="locale">Localization provider for <c>key=</c> text lookups. Defaults to Godot translations.</param>
        public ScenarioConverter(ILocalizationProvider locale = null)
        {
            this.locale = locale ?? GodotTranslationProvider.Instance;
        }

        /// <summary>
        /// Parses raw scenario script text into a dialog queue using the configured locale.
        /// </summary>
        public DialogQueue Convert(string text)
        {
            return ScenarioParser.Parse(text, locale);
        }

        /// <summary>
        /// Reads a scenario file from disk and parses it into a dialog queue.
        /// </summary>
        /// <param name="path">Filesystem path to the <c>.scenario</c> file.</param>
        /// <returns>The parsed <see cref="DialogQueue"/>, or <c>null</c> if the file could not be opened.</returns>
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
