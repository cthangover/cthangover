using System;
using System.Collections.Generic;
using Cthangover.Core.Mods;
using Cthangover.Core.Scenes.Patching;
using Cthangover.Core.Scenes.TscnParsing;

namespace Cthangover.Validator
{
    /// <summary>
    /// Phase: Contracts. Walks every mod's <c>patches/scenes/**</c> tree, parses each patch with
    /// the same <see cref="ScenePatchParser"/> the runtime uses and reports authoring problems:
    /// files that are not inherited scenes, targets no mod provides, attempts to delete the scene
    /// root, malformed connection selectors and unknown <c>metadata/_patch_*</c> keys.
    /// <para/>
    /// Target existence is checked only against mod scenes — native <c>res://scenes/**</c> files
    /// are invisible to the background validator, so a native target is accepted with a note
    /// instead of an error. Deep merge results (whether an override path exists in the target)
    /// cannot be validated without loading the target, which the validator deliberately avoids.
    /// </summary>
    public class ScenePatchStrategy : IValidationStrategy
    {
        private static readonly HashSet<string> KnownReservedKeys = new(StringComparer.Ordinal)
        {
            PatchMetadata.Delete,
            PatchMetadata.Remove,
            PatchMetadata.AddGroups,
            PatchMetadata.RemoveGroups,
            PatchMetadata.DeleteNodes,
            PatchMetadata.RemoveConnections,
            PatchMetadata.RemoveExtResources,
            PatchMetadata.Gc,
        };

        public string Id => "scene_patches";
        public ValidationPhase Phase => ValidationPhase.Contracts;
        public int Order => 110;
        public ValidationFeature Feature => ValidationFeature.Json;

        /// <summary>Validates scene patch .tscn files: known patch operations and target scenes that actually exist.</summary>
        public void Validate(ValidationContext context)
        {
            var sceneKeys = CollectSceneKeys(context);

            foreach (var mod in context.Mods)
            {
                context.ThrowIfCancellationRequested();

                var provider = mod.FileProvider;
                if (provider == null)
                    continue;

                foreach (var file in ModFileReader.ListFiles(provider, ScenePatchResolver.PatchDirectory, true))
                {
                    context.ThrowIfCancellationRequested();
                    if (!file.EndsWith(".tscn", StringComparison.OrdinalIgnoreCase))
                        continue;

                    ValidatePatch(context, mod, provider, file, sceneKeys);
                }
            }
        }

        private static HashSet<string> CollectSceneKeys(ValidationContext context)
        {
            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var mod in context.Mods)
            {
                var provider = mod.FileProvider;
                if (provider == null)
                    continue;

                foreach (var file in ModFileReader.ListFiles(provider, "scenes", true))
                {
                    if (file.EndsWith(".tscn", StringComparison.OrdinalIgnoreCase)
                        || file.EndsWith(".scn", StringComparison.OrdinalIgnoreCase))
                        keys.Add(file.Replace('\\', '/'));
                }
            }
            return keys;
        }

        private static void ValidatePatch(ValidationContext context, IModInfo mod, IModFileProvider provider, string file, HashSet<string> sceneKeys)
        {
            var text = provider.ReadFileText(file);
            if (!ScenePatchParser.TryParse(text, mod.Id, file, out var patch, out var error))
            {
                context.AddError("SCENE_PATCH_INVALID", $"invalid scene patch: {error}", mod.Id, file, 0,
                    "a scene patch is an inherited scene: root node with instance=ExtResource(\"PackedScene\")");
                return;
            }

            if (!TargetExists(patch.TargetPath, sceneKeys))
            {
                context.AddWarning("SCENE_PATCH_TARGET_NOT_FOUND",
                    $"patch target '{patch.TargetPath}' is not provided by any mod (native scene targets are not visible to the validator)",
                    mod.Id, file, 0,
                    "check the ext_resource path or ship the target scene");
            }

            var roots = patch.Scene.Nodes.FindAll(n => n.Parent == null);
            if (roots.Count != 1)
            {
                context.AddError("SCENE_PATCH_ROOT_COUNT", $"patch declares {roots.Count} root nodes, expected exactly one", mod.Id, file, 0);
                return;
            }

            var root = roots[0];
            if (IsTrue(root.Properties.TryGetValue(PatchMetadata.Delete, out var delete) ? delete : null))
            {
                context.AddError("SCENE_PATCH_ROOT_DELETE", "patch tries to delete the scene root", mod.Id, file, 0,
                    "remove the _patch_delete marker from the root node");
            }

            ValidateReservedKeys(context, mod, file, patch);
            ValidateConnectionSelectors(context, mod, file, root);
        }

        private static void ValidateReservedKeys(ValidationContext context, IModInfo mod, string file, ScenePatch patch)
        {
            foreach (var node in patch.Scene.Nodes)
                CheckKeys(context, mod, file, node.Properties?.Keys);

            foreach (var sub in patch.Scene.SubResources)
                CheckKeys(context, mod, file, sub.Properties?.Keys);
        }

        private static void CheckKeys(ValidationContext context, IModInfo mod, string file, IEnumerable<string> keys)
        {
            if (keys == null)
                return;

            foreach (var key in keys)
            {
                if (!key.StartsWith(PatchMetadata.Prefix, StringComparison.Ordinal) || KnownReservedKeys.Contains(key))
                    continue;

                context.AddWarning("SCENE_PATCH_UNKNOWN_KEY", $"unknown reserved scene-patch key '{key}'", mod.Id, file, 0,
                    "reserved keys are listed in the scene patch documentation");
            }
        }

        private static void ValidateConnectionSelectors(ValidationContext context, IModInfo mod, string file, ParsedNode root)
        {
            if (!root.Properties.TryGetValue(PatchMetadata.RemoveConnections, out var value))
                return;

            foreach (var raw in EnumerateValues(value))
            {
                if (!ConnectionSelector.TryParse(raw, out _, out var error))
                {
                    context.AddError("SCENE_PATCH_BAD_SELECTOR", $"invalid connection selector '{raw}': {error}", mod.Id, file, 0,
                        "use key=value pairs separated by ';', e.g. from=Panel/Btn;signal=pressed");
                }
            }
        }

        private static bool TargetExists(string targetPath, HashSet<string> sceneKeys)
        {
            if (string.IsNullOrEmpty(targetPath))
                return false;

            foreach (var key in sceneKeys)
            {
                if (key.Equals(targetPath, StringComparison.OrdinalIgnoreCase)
                    || key.EndsWith("/" + targetPath, StringComparison.OrdinalIgnoreCase)
                    || targetPath.EndsWith("/" + key, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static IEnumerable<string> EnumerateValues(object value)
        {
            switch (value)
            {
                case string[] array:
                    return array;
                case string single:
                    return new[] { single };
                default:
                    return Array.Empty<string>();
            }
        }

        private static bool IsTrue(object value)
        {
            return (value is bool flag && flag)
                || (value is string text && bool.TryParse(text, out var parsed) && parsed);
        }
    }
}
