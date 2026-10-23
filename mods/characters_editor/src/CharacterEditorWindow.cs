using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Cthangover.Core.Characters;
using Cthangover.Core.Mods.Resolvers;
using Cthangover.Tools.EntityEditor;

namespace Cthangover.Tools.CharactersEditor
{
    /// <summary>
    /// Editor of characters (<c>"Kind": "character"</c>, <see cref="CharacterInfo"/>):
    /// heroes, NPCs and enemies alike. Name in every language of the mod, the
    /// portrait (<c>characters</c> group, shown in dialogs and battles), the RPG
    /// map look (<c>Charset</c> in the <c>charsets</c> group: the RPG map editor
    /// places the character with it; a novel leaves it empty), battle stats and the
    /// battle actions offered from the loaded <c>action</c> entities. Loot and
    /// other mods' fields are "other fields" of the base window. New characters go
    /// to <c>characters/&lt;mod&gt;.json</c>.
    /// </summary>
    public partial class CharacterEditorWindow : EntityEditorWindow
    {
        /// <summary>Builds the window and opens the first mod that has characters.</summary>
        public CharacterEditorWindow() : base("tools/characters_editor/title", "character", "characters", Fields())
        {
        }

        private static List<EntityField> Fields() => new()
        {
            new EntityField { Key = "Name", LabelKey = "tools/characters_editor/name", Kind = EntityFieldKind.LocaleText, KeyPattern = "characters/{0}/name" },
            new EntityField { Key = "Image", LabelKey = "tools/characters_editor/image", TooltipKey = "tools/characters_editor/image_tip", Kind = EntityFieldKind.Picture, Group = "characters" },
            new EntityField { Key = "Charset", LabelKey = "tools/characters_editor/charset", TooltipKey = "tools/characters_editor/charset_tip", Kind = EntityFieldKind.Picture, Group = "charsets" },
            new EntityField { Key = "Level", LabelKey = "tools/characters_editor/level", Kind = EntityFieldKind.Integer },
            new EntityField { Key = "Health", LabelKey = "tools/characters_editor/health", Kind = EntityFieldKind.Integer },
            new EntityField { Key = "Attack", LabelKey = "tools/characters_editor/attack", Kind = EntityFieldKind.Integer },
            new EntityField { Key = "Defence", LabelKey = "tools/characters_editor/defence", Kind = EntityFieldKind.Integer },
            new EntityField { Key = "Point", LabelKey = "tools/characters_editor/point", TooltipKey = "tools/characters_editor/point_tip", Kind = EntityFieldKind.Integer },
            new EntityField { Key = "Exp", LabelKey = "tools/characters_editor/exp", TooltipKey = "tools/characters_editor/exp_tip", Kind = EntityFieldKind.Integer },
            new EntityField { Key = "RecruitmentChance", LabelKey = "tools/characters_editor/recruitment", TooltipKey = "tools/characters_editor/recruitment_tip", Kind = EntityFieldKind.Integer },
            new EntityField { Key = "Behaviour", LabelKey = "tools/characters_editor/behaviour", TooltipKey = "tools/characters_editor/behaviour_tip", Kind = EntityFieldKind.Text },
            new EntityField { Key = "Actions", LabelKey = "tools/characters_editor/actions", TooltipKey = "tools/characters_editor/actions_tip", Kind = EntityFieldKind.List, Options = Actions }
        };

        private static IEnumerable<string> Actions()
            => (Entities.Collect<ActionCharacterInfo>()?.Keys ?? Enumerable.Empty<string>()).OrderBy(id => id, StringComparer.Ordinal);

        /// <summary>A level 1 fighter with a plain attack; the portrait is the author's to pick.</summary>
        protected override JsonObject CreateEntity(string id) => new()
        {
            ["Id"] = id,
            ["Behaviour"] = "base",
            ["Level"] = 1,
            ["Point"] = 1,
            ["Health"] = 10,
            ["Defence"] = 0,
            ["Attack"] = 2,
            ["Exp"] = 1,
            ["RecruitmentChance"] = 0,
            ["Actions"] = new JsonArray("physics/attack")
        };

        /// <summary><c>&lt;mod&gt;_new_character</c>: character ids are flat.</summary>
        protected override string NewIdBase(string mod) => mod + "_new_character";
    }
}
