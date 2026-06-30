#if TOOLS
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace SceneManagerAddon
{
    /// <summary>
    /// The validation tab's content — a two-column <see cref="Tree"/>
    /// listing every validation message across all mods, scenes, and
    /// scenarios. Column 0 shows the human-readable message, column 1
    /// shows the source file path. Rows are colour-coded red for errors
    /// and orange for warnings. The tab title is updated with summary
    /// counts (e.g. <c>"Validation (3 err, 1 warn)"</c>). Clicking a
    /// row fires <see cref="ErrorSelected"/> with the file path so
    /// <see cref="MainPanel"/> can navigate to the relevant scene
    /// JSON or scenario script in the Scenes tab.
    /// </summary>
    [Tool]
    public partial class ValidationPanel : Tree
    {
        /// <summary>
        /// Raised when the user clicks a validation error/warning row.
        /// Carries the <see cref="ValidationMessage.FilePath"/> so the
        /// handler can locate and display the offending file.
        /// </summary>
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

        /// <summary>
        /// Clears the tree and populates it with every non-empty
        /// validation message group. Scenes and scenarios with zero
        /// errors are skipped. After populating, the parent
        /// <see cref="TabContainer"/> tab title is updated to show
        /// aggregate error and warning counts.
        /// </summary>
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

            var tab = GetParent<TabContainer>();
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
