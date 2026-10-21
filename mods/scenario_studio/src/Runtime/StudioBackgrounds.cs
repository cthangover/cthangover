namespace Cthangover.Mods.ScenarioStudio
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Cthangover.Core.Actions;
    using Cthangover.Core.Factories.Impls;
    using Cthangover.Core.Mods;
    using Cthangover.Core.Mods.Resolvers;
    using Cthangover.Core.Scenes;
    using Cthangover.Core.UI;
    using Godot;

    /// <summary>
    /// Backgrounds as the studio offers them: ids of the <c>backgrounds</c> file
    /// group of every mod and of the studio layer (the layer wins, see
    /// <see cref="FileListResolver"/>), without the <c>_depth</c>/<c>_albedo</c>
    /// lighting maps that belong to a background.
    /// <para/>
    /// The author drops a picture into a folder mod or into the layer while the
    /// game runs; both the file list of the group and the texture cache of
    /// <see cref="BackgroundFactory"/> were filled before, so <see cref="Reload"/>
    /// drops both and shows the current background again — a replaced picture
    /// (or its depth map) is visible at once, a new one can be picked for a scene.
    /// </summary>
    public static class StudioBackgrounds
    {
        private const string DepthSuffix = "_depth";
        private const string AlbedoSuffix = "_albedo";

        /// <summary>Background ids (group-relative paths without extension), sorted.</summary>
        public static List<string> Ids()
        {
            var ids = new List<string>();
            var files = Files.Collect(BackgroundFactory.Instance.GroupName);
            if (files == null)
                return ids;

            var seen = new HashSet<string>();

            foreach (var key in files.Keys)
            {
                if (!IsTexture(key))
                    continue;
                var id = key.Substring(0, key.Length - Path.GetExtension(key).Length);
                if (!id.EndsWith(DepthSuffix, StringComparison.OrdinalIgnoreCase) && !id.EndsWith(AlbedoSuffix, StringComparison.OrdinalIgnoreCase) && seen.Add(id))
                    ids.Add(id);
            }

            ids.Sort(StringComparer.Ordinal);
            return ids;
        }

        /// <summary>The mod (or the layer) whose file the background <paramref name="id"/> is read from; null when unknown.</summary>
        public static string SourceOf(string id)
        {
            var files = Files.Collect(BackgroundFactory.Instance.GroupName);
            if (files == null)
                return null;
            foreach (var extension in ModConfig.Instance.TextureExtensions)
            {
                if (files.TryGetValue(id + extension, out var entry))
                    return entry.ModId;
            }
            return null;
        }

        /// <summary>
        /// Re-reads the background files: drops the collected file lists and the
        /// cached background textures, then puts the current background and its
        /// lighting maps on screen again from the files as they are now.
        /// </summary>
        public static void Reload()
        {
            ModManager.Instance.GetResolver<FileListResolver>()?.ClearCache();
            BackgroundFactory.Instance.Clear();

            var id = SceneContextNode.LastBackgroundID;
            if (string.IsNullOrEmpty(id))
                return;
            var view = SceneContextNode.FindNode<Node>("ViewBox") as IBackgroundView;
            var texture = BackgroundFactory.Instance.Get(id);
            if (view == null || texture == null)
                return;

            view.SetBackgroundTexture(texture);
            LightingBridge.SetupBackgroundMaps(BackgroundFactory.Instance.Get(id + DepthSuffix), BackgroundFactory.Instance.Get(id + AlbedoSuffix));
        }

        private static bool IsTexture(string key)
        {
            var extension = Path.GetExtension(key);
            foreach (var known in ModConfig.Instance.TextureExtensions)
            {
                if (string.Equals(known, extension, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }
}
