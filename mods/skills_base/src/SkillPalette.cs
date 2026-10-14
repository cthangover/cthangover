using Cthangover.Core.UI.Theming;
using Godot;

namespace Cthangover.Mods.Skills
{
    /// <summary>
    /// Maps skill card enums to their visual identity. The five rarity colours
    /// are fixed by the UI style guide (Common→muted, Uncommon→success,
    /// Rare→info, Epic→recipe, Legendary→accent) and are shared by the grid
    /// card frame, the study plate, the rarity filter tabs and the rarity
    /// badge — a card can never appear in two different colours in one screen.
    /// The type accents reuse the action-card palette so the same "passive",
    /// "active" and "self" semantics read identically across panels, and the
    /// locale keys follow both lists for badge labels.
    /// </summary>
    public static class SkillPalette
    {
        private static readonly Color[] RarityColors =
        {
            UiTokens.Muted,
            UiTokens.Success,
            UiTokens.Info,
            UiTokens.TypeRecipe,
            UiTokens.Accent,
        };

        /// <summary>Rarity colour clipped to the five known tiers; unknown values degrade to Common.</summary>
        public static Color RarityColor(RareType rarity)
        {
            var index = Mathf.Clamp((int)rarity, 0, RarityColors.Length - 1);
            return RarityColors[index];
        }

        /// <summary>Locale key of the rarity name shown on the badge and the rarity filter tabs.</summary>
        public static string RarityLocaleKey(RareType rarity)
            => "ui/skills/rarity/" + rarity.ToString().ToLowerInvariant();

        /// <summary>Accent colour of the skill type badge, matching the action-card badges.</summary>
        public static Color TypeColor(SkillType type)
        {
            switch (type)
            {
                case SkillType.Active:
                    return UiTokens.TypeActive;
                case SkillType.ForCharacter:
                    return UiTokens.TypeSelf;
                default:
                    return UiTokens.TypePassive;
            }
        }

        /// <summary>Locale key of the type name; <c>ForCharacter</c> needs an explicit snake_case key.</summary>
        public static string TypeLocaleKey(SkillType type)
        {
            switch (type)
            {
                case SkillType.Active:
                    return "ui/skills/type/active";
                case SkillType.ForCharacter:
                    return "ui/skills/type/for_character";
                default:
                    return "ui/skills/type/passive";
            }
        }
    }
}
