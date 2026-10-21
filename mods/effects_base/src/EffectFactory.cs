using System;
using System.Collections.Generic;
using Cthangover.Core.Factories;
using Cthangover.Core.Mods;
using Cthangover.Core.Mods.Resolvers;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Mods.Effects
{
    /// <summary>
    /// Factory for <c>PackedScene</c> visual effects. Overrides
    /// <c>LoadFromMods</c> to use <c>ResourceLoader.Load</c> from the file
    /// system instead of <c>ConvertFromBytes</c>, because Godot's scene
    /// format is a proprietary binary blob that cannot be reconstructed
    /// from raw bytes in managed code. This imposes a hard constraint:
    /// effect scenes are only loadable from <b>folder mods</b> — zip mods
    /// skip them with a warning — so mod authors distributing visual effects
    /// must ship them unpacked.
    /// <para/>
    /// The single instance is a self-contained lazy singleton owned by this
    /// mod assembly; consumers resolve it through <see cref="Instance"/>.
    /// </summary>
    public class EffectFactory : PrefabFactory<PackedScene>
    {
        private static readonly Lazy<EffectFactory> instance = new(() => new EffectFactory());

        /// <summary>Process-wide instance; laziness matches the former AppServices slot.</summary>
        public static EffectFactory Instance => instance.Value;

        private EffectFactory() : base("effects", 64) { }

        /// <summary>Mod asset group scanned by this factory — files live under <c>mods/*/effects</c>.</summary>
        public override string GroupName => "effects";

        /// <summary>Scene file extensions accepted during the mod scan.</summary>
        protected override List<string> Extensions { get; } = new() { ".tscn", ".scn" };

        /// <summary>Always fails with a log — PackedScene is a proprietary Godot blob; use <see cref="LoadFromMods"/> (folder mods only).</summary>
        protected override PackedScene ConvertFromBytes(string id, byte[] data, string extension)
        {
            GameLogger.Log("FACTORY", $"PackedScene cannot be constructed from bytes; use folder mods for '{id}'", LogLevel.Error);
            return null;
        }

        /// <summary>
        /// Loads the scene via <c>ResourceLoader</c> from the mod's real file
        /// system path — which only folder mods can provide; zip mods are
        /// skipped with a warning.
        /// </summary>
        protected override PackedScene LoadFromMods(string id, string extension)
        {
            var files = Files.Collect(GroupName);

            string matchedKey = null;
            foreach (var key in files.Keys)
            {
                if (key == id || key.Equals(id + extension, StringComparison.OrdinalIgnoreCase))
                {
                    matchedKey = key;
                    break;
                }
            }

            if (matchedKey == null || !files.TryGetValue(matchedKey, out var entry))
            {
                GameLogger.Log("FACTORY", $"effect '{id}' - not found in group '{GroupName}'!", LogLevel.Error);
                return null;
            }

            var fsPath = ModManager.Instance.GetFileSystemPath(entry.ModId, entry.FullPath);
            if (fsPath == null)
            {
                GameLogger.Log("FACTORY", $"effect '{id}' - only folder mods support PackedScene (zip mod '{entry.ModId}' skipped)", LogLevel.Warning);
                return null;
            }

            return ResourceLoader.Load<PackedScene>(fsPath);
        }

    }
}
