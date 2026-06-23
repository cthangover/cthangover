#if TOOLS
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace SceneManagerAddon
{
    [Tool]
    public partial class ValidationPanel : Tree
    {
        public event ErrorSelectedHandler ErrorSelected;

        public override void _Ready()
        {
            AllowReselect = true;
            Columns = 2;
            SizeFlagsHorizontal = SizeFlags.ExpandFill;
            SizeFlagsVertical = SizeFlags.ExpandFill;
            SetColumnTitle(0, "Message");
            SetColumnTitle(1, "File");
            CellSelected += OnCellSelected;
        }

        public void Populate(List<ModSceneInfo> mods)
        {
            Clear();
            var root = CreateItem();
            int errors = 0, warns = 0;

            foreach (var mod in mods)
            {
                foreach (var scene in mod.Scenes)
                {
                    if (scene.Errors.Count == 0 && scene.Scenarios.All(s => s.Errors.Count == 0))
                        continue;

                    var modItem = CreateItem(root);
                    modItem.SetText(0, $"{mod.ModId}/{scene.Name}");

                    foreach (var e in scene.Errors)
                    {
                        var item = CreateItem(modItem);
                        item.SetText(0, e.Message);
                        item.SetText(1, e.FilePath);
                        item.SetCustomColor(0, e.Severity == SeverityLevel.Error
                            ? new Color(1f, 0.4f, 0.4f)
                            : new Color(1f, 0.8f, 0.3f));
                        item.SetMetadata(0, e.FilePath);
                        if (e.Severity == SeverityLevel.Error) errors++; else warns++;
                    }

                    foreach (var sc in scene.Scenarios)
                    {
                        foreach (var e in sc.Errors)
                        {
                            var item = CreateItem(modItem);
                            item.SetText(0, $"[{sc.Name}] {e.Message}");
                            item.SetText(1, e.FilePath);
                            item.SetCustomColor(0, e.Severity == SeverityLevel.Error
                                ? new Color(1f, 0.4f, 0.4f)
                                : new Color(1f, 0.8f, 0.3f));
                            item.SetMetadata(0, e.FilePath);
                            if (e.Severity == SeverityLevel.Error) errors++; else warns++;
                        }
                    }
                }
            }

            var tab = GetParent<Control>()?.GetParent<TabContainer>();
            if (tab != null)
            {
                for (int i = 0; i < tab.GetTabCount(); i++)
                    if (tab.GetTabTitle(i).ToString().StartsWith("Validation"))
                        tab.SetTabTitle(i, $"Validation ({errors} err, {warns} warn)");
            }
        }

        private void OnCellSelected()
        {
            var item = GetSelected();
            if (item == null) return;
            var meta = item.GetMetadata(0);
            if (meta.VariantType == Variant.Type.String)
                ErrorSelected?.Invoke(meta.AsString());
        }
    }
}
#endif
