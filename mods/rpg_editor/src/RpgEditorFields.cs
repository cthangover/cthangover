using System;
using Cthangover.Mods.Rpg;
using Godot;

namespace Cthangover.Tools.RpgEditor
{
    /// <summary>Small helpers for the editor's property panels: enum pickers and their localized items.</summary>
    public static class RpgEditorFields
    {
        /// <summary>A facing picker; items in <see cref="RpgDirection"/> order.</summary>
        public static OptionButton Direction(Action<RpgDirection> changed)
        {
            var picker = new OptionButton();
            foreach (var key in new[] { "tools/rpg_editor/dir_down", "tools/rpg_editor/dir_left", "tools/rpg_editor/dir_right", "tools/rpg_editor/dir_up" })
                picker.AddItem(TranslationServer.Translate(key));
            picker.ItemSelected += index => changed((RpgDirection)(int)index);
            return picker;
        }

        /// <summary>Items of a picker built from locale keys; the item index is the value.</summary>
        public static OptionButton Choice(string[] keys, Action<int> changed)
        {
            var picker = new OptionButton();
            foreach (var key in keys)
                picker.AddItem(TranslationServer.Translate(key));
            picker.ItemSelected += index => changed((int)index);
            return picker;
        }
    }
}
