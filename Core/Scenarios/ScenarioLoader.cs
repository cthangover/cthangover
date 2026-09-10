using Cthangover.Core.Localization;
using Cthangover.Core.UI.Dialog;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Scenarios
{
    /// <summary>
    /// Loads a <c>.scenario</c> file from disk, reads its text content, and
    /// parses it via <see cref="ScenarioParser.Parse"/> into a <see cref="DialogQueue"/>.
    /// Uses <see cref="GodotTranslationProvider.Instance"/> as the default localization source.
    /// </summary>
    public static class ScenarioLoader
    {
        /// <summary>
        /// Loads and parses a scenario file using the default Godot translation provider.
        /// </summary>
        /// <param name="path">Filesystem path to the <c>.scenario</c> file.</param>
        /// <returns>The parsed <see cref="DialogQueue"/>, or <c>null</c> if the file could not be opened.</returns>
        public static DialogQueue Load(string path)
        {
            return Load(path, GodotTranslationProvider.Instance);
        }

        /// <summary>
        /// Loads and parses a scenario file with a custom localization provider for text lookups.
        /// </summary>
        /// <param name="path">Filesystem path to the <c>.scenario</c> file.</param>
        /// <param name="provider">Localization provider to resolve <c>key=</c> references during parsing.</param>
        /// <returns>The parsed <see cref="DialogQueue"/>, or <c>null</c> if the file could not be opened.</returns>
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
