using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Cthangover.Core.Factories.Impls;
using Cthangover.Core.Items;
using Cthangover.Tools.EntityEditor;

namespace Cthangover.Tools.ItemsEditor
{
    /// <summary>
    /// Editor of items (<c>"Kind": "item"</c>, <see cref="ItemInfo"/>): name and
    /// description in every language of the mod, price, picture in the
    /// <c>items</c> group (the inventory icon), item types and the use action
    /// offered from the actions the loaded mods register. Fields of other mods'
    /// item C# (<c>heal</c>, <c>verb</c>…) are "other fields" of the base window.
    /// New items go to <c>items/&lt;mod&gt;.json</c>.
    /// </summary>
    public partial class ItemEditorWindow : EntityEditorWindow
    {
        /// <summary>Builds the window and opens the first mod that has items.</summary>
        public ItemEditorWindow() : base("tools/items_editor/title", "item", "items", Fields())
        {
        }

        private static List<EntityField> Fields() => new()
        {
            new EntityField { Key = "Name", LabelKey = "tools/items_editor/name", Kind = EntityFieldKind.LocaleText, KeyPattern = "items/{0}/name" },
            new EntityField { Key = "Description", LabelKey = "tools/items_editor/description", Kind = EntityFieldKind.LocaleText, KeyPattern = "items/{0}/desc" },
            new EntityField { Key = "Cost", LabelKey = "tools/items_editor/cost", TooltipKey = "tools/items_editor/cost_tip", Kind = EntityFieldKind.Integer },
            new EntityField { Key = "Sprite", LabelKey = "tools/items_editor/sprite", TooltipKey = "tools/items_editor/sprite_tip", Kind = EntityFieldKind.Picture, Group = "items" },
            new EntityField { Key = "ItemType", LabelKey = "tools/items_editor/type", TooltipKey = "tools/items_editor/type_tip", Kind = EntityFieldKind.Flags, Options = ItemTypes },
            new EntityField { Key = "ItemAction", LabelKey = "tools/items_editor/action", TooltipKey = "tools/items_editor/action_tip", Kind = EntityFieldKind.Choice, Options = Actions }
        };

        private static IEnumerable<string> ItemTypes() => Enum.GetNames(typeof(ItemType)).Where(n => n != nameof(ItemType.None));

        private static IEnumerable<string> Actions() => new[] { "null" }.Concat(ItemActionFactory.Instance.Ids.OrderBy(id => id, StringComparer.Ordinal));

        /// <summary>A resource worth one coin with no use action; the picture is the author's to pick.</summary>
        protected override JsonObject CreateEntity(string id) => new()
        {
            ["Id"] = id,
            ["Cost"] = 1,
            ["ItemType"] = nameof(ItemType.Resource),
            ["ItemAction"] = "null"
        };

        /// <summary><c>&lt;mod&gt;/new_item</c>, the folder-style ids items use.</summary>
        protected override string NewIdBase(string mod) => mod + "/new_item";
    }
}
