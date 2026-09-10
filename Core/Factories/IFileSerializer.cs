namespace Cthangover.Core.Factories
{
    /// <summary>
    /// Deserialization adapter injected into the <c>ModManager</c> pipeline.
    /// Decouples "how to parse a JSON string into <c>T</c>" from the mod
    /// file crawling and caching logic. The optional <c>resourcePath</c>
    /// parameter allows the serializer to resolve relative paths within
    /// the mod's directory tree (e.g. a character JSON that references a
    /// sprite at <c>"./images/portrait.png"</c>).
    /// </summary>
    public interface IFileSerializer<T> where T : class
    {
        T Read(string content, string resourcePath = null);
    }

}
