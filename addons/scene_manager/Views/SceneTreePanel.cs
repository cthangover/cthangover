#if TOOLS
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace SceneManagerAddon
{
    [Tool]
    public partial class SceneTreePanel : Tree
    {
        public event SceneSelectedHandler SceneSelected;
        public event ScenarioSelectedHandler ScenarioSelected;

        private List<ModSceneInfo> _mods;

        public void Populate(List<ModSceneInfo> mods)
        {
            _mods = mods;
            Clear();
            var root = CreateItem();

            foreach (var mod in mods.OrderBy(m => m.ModId))
            {
                var modItem = CreateItem(root);
                modItem.SetText(0, $"[{mod.ModId}]");
                modItem.SetSelectable(0, false);

                foreach (var scene in mod.Scenes.OrderBy(s => s.Name))
                {
                    var sceneItem = CreateItem(modItem);
                    sceneItem.SetText(0, scene.Name);
                    sceneItem.SetSelectable(0, false);
                    if (scene.HasErrors)
                        sceneItem.SetCustomColor(0, ErrorColor);

                    sceneItem.SetMetadata(0, Variant.From(new Godot.Collections.Dictionary
                    {
                        { "kind", "scene" },
                        { "modId", mod.ModId },
                        { "sceneName", scene.Name }
                    }));

                    foreach (var sc in scene.Scenarios.OrderBy(s => s.Priority))
                    {
                        var scItem = CreateItem(sceneItem);
                        scItem.SetText(0, $"{sc.Name} (p:{sc.Priority})");
                        if (sc.Errors.Count > 0)
                            scItem.SetCustomColor(0, WarnColor);

                        scItem.SetMetadata(0, Variant.From(new Godot.Collections.Dictionary
                        {
                            { "kind", "scenario" },
                            { "modId", mod.ModId },
                            { "sceneName", scene.Name },
                            { "scenarioName", sc.Name }
                        }));
                    }
                }
            }
        }

        public void SelectItem(string modId, string sceneName, string scenarioName)
        {
            var r = GetRoot();
            if (r == null) return;

            var modItem = r.GetFirstChild();
            while (modItem != null)
            {
                if (modItem.GetText(0) == $"[{modId}]")
                {
                    var sceneItem = modItem.GetFirstChild();
                    while (sceneItem != null)
                    {
                        if (sceneItem.GetText(0) == sceneName)
                        {
                            var scItem = sceneItem.GetFirstChild();
                            while (scItem != null)
                            {
                                if (scItem.GetText(0).StartsWith(scenarioName + " "))
                                {
                                    scItem.Select(0);
                                    break;
                                }
                                scItem = scItem.GetNext();
                            }
                            break;
                        }
                        sceneItem = sceneItem.GetNext();
                    }
                    break;
                }
                modItem = modItem.GetNext();
            }
        }

        public override void _Ready()
        {
            AllowReselect = true;
            SizeFlagsHorizontal = SizeFlags.ExpandFill;
            CustomMinimumSize = new Vector2(200, 0);
            CellSelected += OnCellSelected;
        }

        private void OnCellSelected()
        {
            var item = GetSelected();
            if (item == null) return;

            var meta = item.GetMetadata(0);
            if (meta.VariantType != Variant.Type.Dictionary) return;

            var dict = meta.AsGodotDictionary();
            if (!dict.TryGetValue("kind", out var kind)) return;

            if (kind.AsString() == "scene")
            {
                var modId = dict["modId"].AsString();
                var sceneName = dict["sceneName"].AsString();
                var scene = FindScene(modId, sceneName);
                if (scene != null) SceneSelected?.Invoke(scene);
            }
            else if (kind.AsString() == "scenario")
            {
                var modId = dict["modId"].AsString();
                var sceneName = dict["sceneName"].AsString();
                var scName = dict["scenarioName"].AsString();
                var sc = FindScenario(modId, sceneName, scName);
                if (sc != null) ScenarioSelected?.Invoke(sc, modId);
            }
        }

        private SceneDefInfo FindScene(string modId, string name)
        {
            return _mods?.FirstOrDefault(m => m.ModId == modId)
                ?.Scenes.FirstOrDefault(s => s.Name == name);
        }

        private ScenarioDefInfo FindScenario(string modId, string sceneName, string scName)
        {
            var scene = FindScene(modId, sceneName);
            return scene?.Scenarios.FirstOrDefault(s => s.Name == scName);
        }

        private static readonly Color ErrorColor = new(1f, 0.4f, 0.4f);
        private static readonly Color WarnColor = new(1f, 0.6f, 0.3f);
    }
}
#endif
