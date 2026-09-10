namespace Cthangover.Core.Localization
{
    /// <summary>
    /// Abstraction over Godot's <c>TranslationServer</c> so that UI and
    /// game logic code never depends on the engine's translation API
    /// directly. The key semantic: <c>Get</c> returns <c>null</c> when
    /// a key is untranslated (rather than returning the raw key itself),
    /// letting callers fall back to a default string or hide an element
    /// instead of showing an untranslated key to the player.
    /// </summary>
    public interface ILocalizationProvider
    {
        string Get(string key);
    }
}
