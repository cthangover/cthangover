using System;
using Cthangover.Core.Characters;
using Cthangover.Core.Factories.Impls;
using Cthangover.Core.Settings;
using Godot;

namespace Cthangover.Core.UI.Inventory
{
    /// <summary>
    /// Displays a vertical list of party members as clickable entries,
    /// allowing the player to select a target for
    /// <see cref="ItemType.TargetUsed"/> items. Each entry shows the
    /// translated character name. Clicking an entry fires
    /// <see cref="CharacterSelected"/>; the "Cancel" button fires
    /// <see cref="Cancelled"/>. The panel is built entirely in code and
    /// expects to be added as a child of the inventory right panel,
    /// where it takes up the remaining vertical space.
    /// </summary>
    public partial class CharacterTargetPanel : Control
    {
        /// <summary>Raised when the player selects a target character.</summary>
        public event Action<CharacterInfoData> CharacterSelected;
        /// <summary>Raised when the player cancels target selection.</summary>
        public event Action Cancelled;

        /// <summary>
        /// Constructs the panel: a title label, a scrollable list of
        /// party member entries, and a cancel button. Reads the current
        /// party roster from <see cref="CharacterData.BattleSet"/> and
        /// <see cref="CharacterData.Characters"/>. Characters with
        /// missing or dead entries are skipped.
        /// </summary>
        public CharacterTargetPanel()
        {
            SizeFlagsVertical = SizeFlags.ExpandFill;
            MouseFilter = MouseFilterEnum.Stop;

            var title = new Label();
            title.Text = TranslationServer.Translate("ui/inventory/select_target");
            title.AddThemeFontSizeOverride("font_size", 16);
            title.HorizontalAlignment = HorizontalAlignment.Center;
            AddChild(title);

            var scroll = new ScrollContainer();
            scroll.SizeFlagsVertical = SizeFlags.ExpandFill;
            scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
            AddChild(scroll);

            var listContainer = new VBoxContainer();
            listContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            listContainer.AddThemeConstantOverride("separation", 4);
            scroll.AddChild(listContainer);

            var charData = GameData.Instance.Runtime.CharacterData;
            foreach (var id in charData.BattleSet)
            {
                if (!charData.Characters.TryGetValue(id, out var info))
                    continue;
                
                var template = CharacterFactory.Instance.Get(id);
                var rawName = template?.Name ?? id;

                var entry = new Button();
                entry.Text = TranslationServer.Translate(rawName);
                entry.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                entry.FocusMode = FocusModeEnum.None;
                
                var captured = info;
                entry.Pressed += () =>
                {
                    CharacterSelected?.Invoke(captured);
                };

                listContainer.AddChild(entry);
            }

            var cancelBtn = new Button();
            cancelBtn.Text = TranslationServer.Translate("ui/inventory/cancel");
            cancelBtn.Pressed += () =>
            {
                Cancelled?.Invoke();
            };
            AddChild(cancelBtn);
        }
    }
}
