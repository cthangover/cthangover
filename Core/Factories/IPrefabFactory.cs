namespace Cthangover.Core.Factories
{
    /// <summary>
    /// Contract for factories that produce binary assets (Textures, AudioStreams,
    /// PackedScenes) from raw files stored in mod archives. Distinct from
    /// <c>IFileFactory</c> because the input is a byte array — not JSON text —
    /// and the conversion logic depends on the file extension.
    /// </summary>
    public interface IPrefabFactory<out T>
    {
        string GroupName { get; }

        T Get(string id);
    }
}
