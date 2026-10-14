using System;
using System.Collections.Generic;
using System.Text;
using Cthangover.Core.Characters;
using Cthangover.Core.UI.Theming;
using Godot;

using Cthangover.Mods.Interface.Theming;
namespace Cthangover.Core.UI.CharacterPanel
{
    /// <summary>
    /// Shared presentation rules for battle action cards, used by the card,
    /// the slot, the pool and the panel legend so they can never disagree
    /// about an action's colour, badge caption, cost or cooldown. The action
    /// model itself is deliberately schema-free — <see cref="ActionCharacter.Properties"/>
    /// is a free-form string bag — so all "what to show" decisions live here
    /// rather than on the model: known keys (<c>RequiredPoint</c>,
    /// <c>ActiveCooldown</c>) get typed rendering, unknown keys are listed
    /// verbatim. The palette is a thin alias layer over <see cref="UiTokens"/>
    /// so the panel shares the central design system.
    /// </summary>
    public static class ActionCharacterUi
    {
        /// <summary>
        /// Filter tabs of the action pool: <c>null</c> type means "all".
        /// Order defines how the tabs are presented to the player.
        /// </summary>
        public static readonly (ActionCharacterType? Type, string Key)[] Filters =
        {
            (null, "ui/character_panel/filter_all"),
            (ActionCharacterType.ToEnemy, "ui/character_panel/type_enemy"),
            (ActionCharacterType.ToAlias, "ui/character_panel/type_alias"),
            (ActionCharacterType.ToSelf, "ui/character_panel/type_self"),
            (ActionCharacterType.Passive, "ui/character_panel/type_passive"),
            (ActionCharacterType.Active, "ui/character_panel/type_active"),
        };

        /// <summary>Legend order of action types; matches <see cref="Filters"/> minus "all".</summary>
        public static readonly ActionCharacterType[] LegendTypes =
        {
            ActionCharacterType.ToEnemy,
            ActionCharacterType.ToAlias,
            ActionCharacterType.ToSelf,
            ActionCharacterType.Passive,
            ActionCharacterType.Active,
        };

        /// <summary>Standard text alias for card body copy.</summary>
        public static readonly Color Ink = UiTokens.Ink;
        /// <summary>Title ink alias for card names.</summary>
        public static readonly Color InkBright = UiTokens.InkBright;
        /// <summary>Body text alias (descriptions and properties).</summary>
        public static readonly Color Body = UiTokens.Body;
        /// <summary>Muted text alias (badges and legends).</summary>
        public static readonly Color Muted = UiTokens.Muted;
        /// <summary>Secondary text alias (captions and metadata).</summary>
        public static readonly Color Faint = UiTokens.Faint;
        /// <summary>Primary accent alias for active card elements.</summary>
        public static readonly Color Gold = UiTokens.Accent;
        /// <summary>Bright accent alias for emphasized text and glyphs.</summary>
        public static readonly Color GoldBright = UiTokens.AccentBright;
        /// <summary>Default hairline border alias.</summary>
        public static readonly Color Line = UiTokens.Line;
        /// <summary>Card border alias.</summary>
        public static readonly Color CardLine = UiTokens.LineCard;
        /// <summary>Card fill alias.</summary>
        public static readonly Color CardBg = UiTokens.Surface1;
        /// <summary>List cell fill alias.</summary>
        public static readonly Color CellBg = UiTokens.Surface2;
        /// <summary>Danger accent alias for cost and damage captions.</summary>
        public static readonly Color Danger = UiTokens.Danger;

        /// <summary>Type accent colour following the mockup palette: enemy, ally, self, passive, active.</summary>
        public static Color AccentColor(ActionCharacterType type)
        {
            switch (type)
            {
                case ActionCharacterType.ToEnemy:
                    return UiTokens.TypeEnemy;
                case ActionCharacterType.ToAlias:
                    return UiTokens.TypeTarget;
                case ActionCharacterType.ToSelf:
                    return UiTokens.TypeSelf;
                case ActionCharacterType.Passive:
                    return UiTokens.TypePassive;
                case ActionCharacterType.Active:
                    return UiTokens.TypeActive;
                default:
                    return UiTokens.Muted;
            }
        }

        /// <summary>Locale key of the badge caption for an action type.</summary>
        public static string TypeLabelKey(ActionCharacterType type)
        {
            switch (type)
            {
                case ActionCharacterType.ToEnemy:
                    return "ui/character_panel/type_enemy";
                case ActionCharacterType.ToAlias:
                    return "ui/character_panel/type_alias";
                case ActionCharacterType.ToSelf:
                    return "ui/character_panel/type_self";
                case ActionCharacterType.Passive:
                    return "ui/character_panel/type_passive";
                case ActionCharacterType.Active:
                    return "ui/character_panel/type_active";
                default:
                    return "ui/character_panel/type_self";
            }
        }

        /// <summary>
        /// Action point cost read from the free-form property bag. Absent or
        /// non-positive values mean the action is free to slot (passive/active
        /// cards authored without a battle cost).
        /// </summary>
        public static int CostOf(ActionCharacter action)
        {
            if (action?.Properties == null)
                return 0;
            return action.GetInt("RequiredPoint", 0);
        }

        /// <summary>Loc-format cost caption ("1 ОД"), or <c>null</c> when the action has no cost.</summary>
        public static string CostText(ActionCharacter action)
        {
            var cost = CostOf(action);
            return cost > 0 ? string.Format(Translate("ui/character_panel/cost"), cost) : null;
        }

        /// <summary>
        /// Seconds left before an <see cref="ActionCharacterType.Active"/> card
        /// can be used again. Zero for every other type, for cards that were
        /// never used (<c>LastUsed</c> absent) and for cards without a positive
        /// <c>ActiveCooldown</c>.
        /// </summary>
        public static long CooldownRemaining(ActionCharacter action)
        {
            if (action == null || action.Type != ActionCharacterType.Active || action.Properties == null)
                return 0;
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            return action.GetRemainingCooldown(now);
        }

        /// <summary>Cooldown badge caption ("КД 42:10") or <c>null</c> when the card is ready.</summary>
        public static string CooldownText(ActionCharacter action)
        {
            var remaining = CooldownRemaining(action);
            return remaining > 0
                ? string.Format(Translate("ui/character_panel/cooldown"), FormatDuration(remaining))
                : null;
        }

        /// <summary>Formats seconds as <c>M:SS</c> or <c>H:MM:SS</c> for long cooldowns.</summary>
        public static string FormatDuration(long seconds)
        {
            var span = TimeSpan.FromSeconds(Mathf.Max(0, seconds));
            return span.TotalHours >= 1
                ? $"{(int)span.TotalHours}:{span.Minutes:00}:{span.Seconds:00}"
                : $"{span.Minutes:00}:{span.Seconds:00}";
        }

        /// <summary>
        /// Property entries of an action sorted by key for a stable display.
        /// Keys are free-form mod data, so no whitelist filtering is applied.
        /// </summary>
        public static List<KeyValuePair<string, string>> SortedProperties(ActionCharacter action)
        {
            var result = new List<KeyValuePair<string, string>>();
            if (action?.Properties?.Values == null)
                return result;

            foreach (var pair in action.Properties.Values)
                result.Add(pair);
            result.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
            return result;
        }

        /// <summary>Plain tooltip assembled from the card fields; markup tags are stripped.</summary>
        public static string BuildTooltip(ActionCharacter action)
        {
            if (action == null)
                return string.Empty;

            var builder = new StringBuilder();
            builder.AppendLine(Translate(action.Name));
            builder.AppendLine(Translate(TypeLabelKey(action.Type)) + " · " + action.ID);

            var description = StripMarkup(Translate(action.Description));
            if (!string.IsNullOrEmpty(description))
                builder.AppendLine(description);

            var properties = SortedProperties(action);
            if (properties.Count > 0)
            {
                builder.AppendLine();
                foreach (var pair in properties)
                    builder.AppendLine($"{pair.Key}: {pair.Value}");
            }

            return builder.ToString().TrimEnd();
        }

        /// <summary>Removes view tags (e.g. <c>&lt;g&gt;1.0&lt;/g&gt;</c>) from localized descriptions.</summary>
        public static string StripMarkup(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            var builder = new StringBuilder(text.Length);
            var inTag = false;
            foreach (var symbol in text)
            {
                if (symbol == '<')
                {
                    inTag = true;
                    continue;
                }
                if (symbol == '>')
                {
                    inTag = false;
                    continue;
                }
                if (!inTag)
                    builder.Append(symbol);
            }
            return builder.ToString();
        }

        /// <summary>Translates a locale key, falling back to the key itself for literals.</summary>
        public static string Translate(string keyOrLiteral)
        {
            if (string.IsNullOrEmpty(keyOrLiteral))
                return string.Empty;
            return TranslationServer.Translate(keyOrLiteral).ToString();
        }

        /// <summary>Small bordered caption chip used for types, costs and cooldowns.</summary>
        public static Control MakeBadge(string text, Color color, int fontSize = 10)
            => UiKit.Badge(text, color, fontSize);

        /// <summary>Property chip ("Attack 1.0") for the expanded card body.</summary>
        public static Control MakePropertyChip(string key, string value)
            => UiKit.PropertyChip(key, value);

        /// <summary>Standard dark frame for cards, slots and list rows.</summary>
        public static StyleBoxFlat MakeFrame(Color border, Color background, int radius = 6, int borderWidth = 1)
            => UiKit.Frame(radius, background, border, borderWidth);

        /// <summary>Convenience label factory mirroring the inventory panel's styling.</summary>
        public static Label MakeLabel(string text, int size, Color color)
            => UiKit.Label(text, size, color);
    }
}
