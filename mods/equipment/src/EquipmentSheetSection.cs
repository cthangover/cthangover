namespace Cthangover.Mods.Equipment
{
    using System.Linq;
    using Cthangover.Core.UI.Theming;
    using Cthangover.Core.Utils;
    using Cthangover.Mods.Interface.Theming;
    using Cthangover.Mods.Stats;
    using Godot;

    /// <summary>
    /// The Equipment block of the character sheet: one row per slot with what is worn
    /// there (picture, name, what it gives) and a button to take it off. Putting gear on
    /// stays in the bag, where every item already has its own action, so the sheet does
    /// not need an item picker of its own.
    /// </summary>
    public class EquipmentSheetSection : ISheetSection
    {
        /// <inheritdoc />
        public string Id => "equipment";

        /// <inheritdoc />
        public int Order => 10;

        /// <inheritdoc />
        public string TitleKey => "equipment/sheet/title";

        /// <inheritdoc />
        public bool AppliesTo(string characterId) => EquipmentService.Slots.Count > 0;

        /// <inheritdoc />
        public void Build(VBoxContainer container, string characterId)
        {
            foreach (var slot in EquipmentService.Slots)
                container.AddChild(SlotRow(characterId, slot));
            container.AddChild(UiKit.Hint(Tr("equipment/sheet/hint")));
        }

        private static Control SlotRow(string characterId, EquipSlotInfo slot)
        {
            var row = UiKit.Row(UiTokens.Space3);
            row.Name = "Slot_" + slot.ID;
            var name = UiKit.Label(Tr(slot.Name ?? slot.ID), UiTokens.FontBody, UiTokens.Body);
            name.CustomMinimumSize = new Vector2(170, 0);
            row.AddChild(name);

            var itemId = EquipmentService.ItemIn(characterId, slot.ID);
            var item = EquipmentService.Item(itemId);
            if (item == null)
            {
                row.AddChild(UiKit.Label("—", UiTokens.FontBody, UiTokens.Faint));
                return row;
            }

            var icon = string.IsNullOrEmpty(item.Sprite) ? null : TextureUtils.LoadFromModGroup("items", item.Sprite);
            if (icon != null)
            {
                var picture = UiKit.Icon(icon, 32);
                picture.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
                row.AddChild(picture);
            }
            var label = UiKit.Label(Tr(item.Name), UiTokens.FontBodyLarge, UiTokens.InkBright);
            label.TooltipText = string.IsNullOrEmpty(item.Description) ? string.Empty : Tr(item.Description);
            label.MouseFilter = Control.MouseFilterEnum.Pass;
            row.AddChild(label);
            var traits = ExtraData.Traits(item.ExtraProperties, item.ID).Where(t => t.Add != 0 || t.Percent != 0).ToList();
            if (traits.Count > 0)
                row.AddChild(UiKit.Label(string.Join("  ", traits.Select(TraitText)), UiTokens.FontSmall, UiTokens.SuccessText));

            var off = UiKit.Button(Tr("equipment/sheet/take_off"));
            off.Name = "TakeOff";
            off.Pressed += () => EquipmentService.Unequip(characterId, slot.ID);
            row.AddChild(off);
            return row;
        }

        private static string TraitText(TraitInfo trait)
        {
            var def = StatsService.Definition(trait.Stat);
            var stat = def == null ? trait.Stat : Tr(string.IsNullOrEmpty(def.Short) ? def.Name : def.Short);
            return trait.Percent != 0
                ? $"{stat} {(trait.Percent > 0 ? "+" : "")}{trait.Percent:0.#}%"
                : $"{stat} {(trait.Add > 0 ? "+" : "")}{trait.Add:0.#}";
        }

        private static string Tr(string key) => TranslationServer.Translate(key).ToString();
    }
}
