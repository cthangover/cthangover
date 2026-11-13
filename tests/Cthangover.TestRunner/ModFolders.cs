namespace Cthangover.TestRunner
{
    /// <summary>
    /// The project folders that hold mods — the runner's mirror of the core's
    /// <c>Cthangover.Core.Mods.ModRootFolders</c> (the runner does not reference
    /// the core assembly; <c>ModRootFoldersTests</c> fails when the two lists
    /// differ). Everything the runner does per mod goes through this list, so a
    /// mod is copied into the test workspace, catalogued and scheduled the same
    /// way whichever folder it lies in.
    /// <para/>
    /// A folder may carry its own <see cref="ManifestFile"/> with the integration
    /// tests and manual-suite policies of its mods (see
    /// <see cref="TestManifest.LoadProject"/>): tests of mods that are not part of
    /// the public repository stay next to those mods instead of in
    /// <c>tests/tests.manifest.json</c>.
    /// </summary>
    public static class ModFolders
    {
        /// <summary>The public mods folder.</summary>
        public const string Primary = "mods";

        /// <summary>Name of the tests manifest, both the project's own and a mods folder's part.</summary>
        public const string ManifestFile = "tests.manifest.json";

        /// <summary>Every mods folder of the project, <see cref="Primary"/> first.</summary>
        public static IReadOnlyList<string> Names { get; } = new[] { Primary, "mods_private" };
    }
}
