using Godot;

namespace Cthangover.Core.Scenes.TscnBuild
{
    /// <summary>
    /// Builds a Godot <see cref="Resource"/> from raw bytes read through <see cref="Mods.IModFileProvider"/>.
    /// Each implementation handles exactly one ext_resource type (e.g. "Texture2D").
    /// <para/>
    /// Unlike <see cref="ISubResourceBuilder"/> which receives a parsed <c>[sub_resource]</c> definition,
    /// ext-resource builders receive raw file bytes — the resource is loaded from a mod file, not defined inline.
    /// <para/>
    /// Auto-discovered via <c>Reflections.FindAndCreate&lt;IExtResourceBuilder&gt;()</c>.
    /// To add support for a new resource type, implement this interface in any loaded assembly — no registration code needed.
    /// </summary>
    public interface IExtResourceBuilder
    {
        /// <summary>Resource type name as it appears in the [ext_resource] header (e.g. "Texture2D").</summary>
        string ResourceType { get; }

        /// <summary>Creates the Godot resource from raw file bytes. The <paramref name="path"/> is the original mod-relative path (may be used for extension detection).</summary>
        Resource Build(byte[] data, string path);
    }
}
