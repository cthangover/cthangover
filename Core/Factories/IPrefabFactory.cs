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
        /// <summary>Mod asset group this factory scans; ids are relative to it.</summary>
        string GroupName { get; }

        /// <summary>Returns the cached asset for <paramref name="id"/>, or <c>null</c> when missing.</summary>
        T Get(string id);
    }
}
