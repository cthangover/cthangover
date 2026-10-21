namespace Cthangover.Mods.ScenarioStudio
{
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using Cthangover.Core.Bootstrap;
    using Cthangover.Core.Games;
    using Cthangover.Core.Mods;
    using Cthangover.Core.Mods.Providers;
    using Cthangover.Core.Mods.Resolvers;
    using Cthangover.Core.Scenes;
    using Cthangover.Core.Utils;
    using Godot;

    /// <summary>
    /// The studio's edit layer: a folder per game (<c>user://studio/&lt;game&gt;/</c>,
    /// or <c>--studio-layer=&lt;dir&gt;</c>) laid out like a mod (<c>scenarios/</c>,
    /// <c>scenes/</c>, <c>backgrounds/</c>) and mounted as content overlay <see cref="Id"/> through
    /// <see cref="ModOverlays"/>. Scenarios written here override same-named
    /// scenarios of any mod, so a player can change content of read-only zip
    /// mods without touching them; new scenes and scenarios live here too, and
    /// pictures the author drops into <c>backgrounds/</c> become backgrounds
    /// after <see cref="StudioBackgrounds.Reload"/>.
    /// <para/>
    /// Edits of folder mods go into the mod itself (the author's workflow)
    /// unless <c>--studio-write=layer</c> forces the layer — tests use it to
    /// keep the repository untouched. <see cref="Export"/> turns the layer into
    /// a regular mod in <c>user://mods/</c>.
    /// </summary>
    public sealed class StudioLayer
    {
        public const string Id = "my_studio";
        private const string LayerArg = "--studio-layer=";
        private const string WriteArg = "--studio-write=layer";

        /// <summary>Absolute path of the layer folder.</summary>
        public string RootPath { get; }

        /// <summary>True when every edit goes to the layer, folder mods included.</summary>
        public bool ForceLayer { get; }

        public StudioLayer(string[] args)
        {
            string dir = null;
            foreach (var arg in args)
            {
                if (arg.StartsWith(LayerArg, System.StringComparison.Ordinal))
                    dir = arg.Substring(LayerArg.Length);
                if (arg == WriteArg)
                    ForceLayer = true;
            }

            dir ??= "user://studio";
            var game = string.IsNullOrEmpty(ActiveGame.Id) ? "dev" : ActiveGame.Id;
            RootPath = Path.Combine(ProjectSettings.GlobalizePath(dir), game);
        }

        /// <summary>Creates the folders if needed, mounts it as overlay and refreshes scenario and scene tables.</summary>
        public void Mount()
        {
            Directory.CreateDirectory(Path.Combine(RootPath, "scenarios"));
            Directory.CreateDirectory(Path.Combine(RootPath, "scenes"));
            Directory.CreateDirectory(Path.Combine(RootPath, "backgrounds"));
            ModOverlays.Mount(Id, new FolderModFileProvider(RootPath, Id));
            RefreshContent();
            GameLogger.Log("UI", $"Scenario studio: layer '{Id}' mounted at {RootPath}");
        }

        /// <summary>Writes a layer file (mod-relative path) and returns its absolute path.</summary>
        public string Write(string relativePath, string text)
        {
            var path = Path.Combine(RootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, text);
            return path;
        }

        /// <summary>
        /// Creates scene <paramref name="name"/> with the given backgrounds and a
        /// first scenario for it; returns the scenario name.
        /// </summary>
        public string CreateScene(string name, IReadOnlyList<string> backgrounds, string firstLine)
        {
            var json = new StringBuilder("{\n  \"name\": \"").Append(name).Append('"');
            if (backgrounds != null && backgrounds.Count > 0)
            {
                json.Append(",\n  \"defaultBackground\": [");
                for (var i = 0; i < backgrounds.Count; i++)
                    json.Append(i == 0 ? "" : ", ").Append('"').Append(backgrounds[i]).Append('"');
                json.Append(']');
            }
            json.Append("\n}\n");
            Write($"scenes/{name}.json", json.ToString());
            return CreateScenario(name, firstLine);
        }

        /// <summary>Creates a new scenario for scene <paramref name="scene"/> with one line; returns its name.</summary>
        public string CreateScenario(string scene, string firstLine)
        {
            var name = scene + "_studio";
            for (var n = 2; File.Exists(ScenarioFile(name)); n++)
                name = $"{scene}_studio_{n}";

            Write($"scenarios/{name}.scenario", $"scene: {scene}\npriority: 50\n---\n{firstLine}\nend\n");
            RefreshContent();
            return name;
        }

        /// <summary>Scene names defined by the layer itself.</summary>
        public List<string> SceneNames()
        {
            var result = new List<string>();
            var dir = Path.Combine(RootPath, "scenes");
            if (!Directory.Exists(dir))
                return result;
            foreach (var file in Directory.GetFiles(dir, "*.json"))
                result.Add(Path.GetFileNameWithoutExtension(file));
            return result;
        }

        /// <summary>
        /// Copies the layer into <c>user://mods/&lt;modId&gt;/</c> with a manifest, so
        /// the edits become a regular mod that can be added to a game's collection.
        /// Returns the folder written.
        /// </summary>
        public string Export(string modId)
        {
            var target = Path.Combine(ProjectSettings.GlobalizePath("user://mods/"), modId);
            CopyTree(RootPath, target);
            var manifest = "{\n" +
                $"  \"id\": \"{modId}\",\n" +
                $"  \"name\": \"{modId}\",\n" +
                "  \"version\": \"0.1.0\",\n" +
                "  \"author\": \"studio\",\n" +
                "  \"description\": \"Exported from the scenario studio layer.\",\n" +
                "  \"category\": \"content\",\n" +
                "  \"depends\": [\"dialog_base>=1\"]\n" +
                "}\n";
            File.WriteAllText(Path.Combine(target, "manifest.json"), manifest);
            GameLogger.Log("UI", $"Scenario studio: layer exported as mod '{modId}' to {target}");
            return target;
        }

        /// <summary>Makes the next scene composition and the scene table see changed scenario and scene files.</summary>
        public static void RefreshContent()
        {
            ScenarioDefs.Invalidate();
            ModManager.Instance.GetResolver<SceneDefinitionResolver>()?.ClearCache();
            AppServices.Current.SceneDefs.Clear();
            SceneManager.RescanScenes();
        }

        private string ScenarioFile(string name) => Path.Combine(RootPath, "scenarios", name + ".scenario");

        private static void CopyTree(string from, string to)
        {
            Directory.CreateDirectory(to);
            foreach (var file in Directory.GetFiles(from))
                File.Copy(file, Path.Combine(to, Path.GetFileName(file)), true);
            foreach (var dir in Directory.GetDirectories(from))
                CopyTree(dir, Path.Combine(to, Path.GetFileName(dir)));
        }
    }
}
