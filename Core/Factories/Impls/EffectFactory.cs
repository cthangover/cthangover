using System;
using System.Collections.Generic;
using Cthangover.Core.Mods;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Factories.Impls
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
    /// </summary>
    public class EffectFactory : PrefabFactory<PackedScene>
    {
        private static readonly Lazy<EffectFactory> instance = new(() => new EffectFactory());

        private EffectFactory() : base("effects", 64) { }

        public static EffectFactory Instance => instance.Value;

        public override string GroupName => "effects";

        protected override List<string> Extensions { get; } = new() { ".tscn", ".scn" };
        
        protected override PackedScene ConvertFromBytes(string id, byte[] data, string extension)
        {
            GameLogger.Log("FACTORY", $"PackedScene cannot be constructed from bytes; use folder mods for '{id}'", LogLevel.Error);
            return null;
        }

        protected override PackedScene LoadFromMods(string id, string extension)
        {
            var files = ModManager.Instance.CollectFileList(GroupName);

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
