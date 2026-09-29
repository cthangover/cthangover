using System;
using System.Collections.Generic;
using System.Linq;
using Cthangover.Core.Scenes.TscnParsing;

namespace Cthangover.Core.Scenes.Patching
{
    /// <summary>
    /// Parses patch text into a <see cref="ScenePatch"/>. A patch is a regular .tscn file
    /// parsed by the same <see cref="TscnParser"/> as any scene; the only extra contract is
    /// the root node: it must carry an <c>instance=ExtResource(...)</c> reference to a
    /// <c>PackedScene</c> ext_resource, which is simultaneously the patch header, the editor's
    /// inherited-scene marker and the machine-readable target. Files without that shape are
    /// not patches (they may be ordinary scenes that happen to live under patches/scenes).
    /// </summary>
    public static class ScenePatchParser
    {
        /// <summary>
        /// Parses <paramref name="text"/>. Returns false with a diagnostic
        /// <paramref name="error"/> when the file is not a valid patch.
        /// </summary>
        public static bool TryParse(string text, string modId, string sourcePath, out ScenePatch patch, out string error)
        {
            patch = null;
            error = null;

            if (string.IsNullOrWhiteSpace(text))
            {
                error = "patch file is empty";
                return false;
            }

            ParsedScene scene;
            try
            {
                scene = new TscnParser().Parse(text);
            }
            catch (Exception ex)
            {
                error = $"parse failed: {ex.Message}";
                return false;
            }

            var root = scene.Nodes.FirstOrDefault(n => n.Parent == null);
            if (root == null)
            {
                error = "patch has no root node";
                return false;
            }

            if (string.IsNullOrEmpty(root.InstanceRef))
            {
                error = "patch root must instance the target scene (instance=ExtResource(...))";
                return false;
            }

            var refId = ExtractResourceId(root.InstanceRef);
            var ext = scene.ExtResources.FirstOrDefault(e => string.Equals(e.Id, refId, StringComparison.Ordinal));
            if (ext == null)
            {
                error = $"root instance references unknown ext_resource '{refId}'";
                return false;
            }

            if (!string.Equals(ext.Type, "PackedScene", StringComparison.OrdinalIgnoreCase))
            {
                error = $"root instance must reference a PackedScene, got '{ext.Type}'";
                return false;
            }

            var target = ScenePatchTargetResolver.Normalize(ext.Path);
            if (target.Length == 0)
            {
                error = "target scene path is empty";
                return false;
            }

            patch = new ScenePatch
            {
                ModId = modId,
                SourcePath = sourcePath,
                TargetPath = target,
                TargetExtResourceId = ext.Id,
                Scene = scene
            };
            return true;
        }

        /// <summary>
        /// Extracts the local id from an <c>ExtResource("id")</c> value. Returns the raw
        /// trimmed string when the value has no wrapper so malformed input still produces a
        /// readable lookup error.
        /// </summary>
        private static string ExtractResourceId(string refString)
        {
            if (string.IsNullOrEmpty(refString))
                return null;

            const string prefix = "ExtResource(";
            var start = refString.IndexOf(prefix, StringComparison.Ordinal);
            if (start < 0)
                return refString.Trim('"');

            start += prefix.Length;
            var end = refString.IndexOf(')', start);
            if (end < 0)
                return refString.Trim('"');

            return refString.Substring(start, end - start).Trim().Trim('"');
        }
    }
}
