namespace Cthangover.Mods.StatChecks
{
    using System.Collections.Generic;
    using System.Globalization;
    using Cthangover.Core.Games;
    using Cthangover.Core.Localization;
    using Cthangover.Mods.Dialog;
    using Cthangover.Mods.Dialog.Scenarios;
    using Cthangover.Mods.Stats;
    using Godot;

    /// <summary>
    /// The DSL command of a stat check:
    /// <code>check cha 12 [who=player] [dice=d20] [bonus=2] [else=fail] [quiet=1] -> :success</code>
    /// rolls stat + dice + bonus against the difficulty, shows one line with the outcome
    /// ("[Charisma 15 / 12] Success") unless <c>quiet</c>, and jumps to the arrow label on
    /// success, to <c>else</c> on failure (or goes on with the next line).
    /// <para/>
    /// The same check serves a visual novel ("persuade the guard"), a tile RPG ("force the
    /// rusty door") and any other game with stats: the command only knows the stats
    /// facade. The dice default is the game's setting <c>checks.dice</c>, else <c>d20</c>;
    /// <c>dice=none</c> makes it a plain threshold. After the line the dialog variables
    /// <c>check_value</c>, <c>check_roll</c>, <c>check_bonus</c>, <c>check_total</c>,
    /// <c>check_dc</c>, <c>check_stat</c> and <c>check_result</c> hold the roll for
    /// <c>ptext</c> lines that want to tell it their own way.
    /// </summary>
    public class CheckCommandStrategy : IScenarioCommandStrategy, IScenarioCommandSpec
    {
        /// <summary>Default line when the game has no <c>checks/line</c> translation.</summary>
        public const string DefaultLine = "[${check_stat} ${check_total} / ${check_dc}] ${check_result}";

        /// <inheritdoc />
        public string Command => "check";

        /// <inheritdoc />
        public int MinPositional => 2;

        /// <inheritdoc />
        public int MaxPositional => 2;

        /// <inheritdoc />
        public IReadOnlyList<string> AllowedNamed => new[] { "who", "dice", "bonus", "else", "quiet" };

        /// <inheritdoc />
        public string JumpTargetNamedKey => "else";

        /// <inheritdoc />
        public void Execute(DialogQueue dlg, List<string> positional, Dictionary<string, string> named, string arrowTarget, ILocalizationProvider locale)
        {
            named.TryGetValue("who", out var who);
            named.TryGetValue("dice", out var dice);
            named.TryGetValue("bonus", out var bonus);
            named.TryGetValue("else", out var otherwise);
            var check = new StatCheck
            {
                Who = who,
                Stat = positional.Count > 0 ? positional[0].ToLowerInvariant() : string.Empty,
                Difficulty = Int(positional.Count > 1 ? positional[1] : null),
                Dice = dice ?? DefaultDice(),
                Bonus = Int(bonus),
            };

            dlg.Run(check.Perform);
            dlg.Set("check_stat", () => StatName(check.Stat));
            dlg.Set("check_value", () => check.Value.ToString(CultureInfo.InvariantCulture));
            dlg.Set("check_roll", () => check.Roll.ToString(CultureInfo.InvariantCulture));
            dlg.Set("check_bonus", () => check.Bonus.ToString(CultureInfo.InvariantCulture));
            dlg.Set("check_total", () => check.Total.ToString(CultureInfo.InvariantCulture));
            dlg.Set("check_dc", () => check.Difficulty.ToString(CultureInfo.InvariantCulture));
            dlg.Set("check_result", () => Tr(check.Success ? "checks/success" : "checks/failure"));
            if (!named.ContainsKey("quiet"))
                dlg.PText(locale?.Get("checks/line") ?? DefaultLine);
            dlg.IfGoTo(() => check.Success, Label(arrowTarget), Label(otherwise));
        }

        private static string DefaultDice()
        {
            var setting = ActiveGame.GetSetting("checks.dice");
            return string.IsNullOrWhiteSpace(setting) ? "d20" : setting;
        }

        private static int Int(string text)
            => int.TryParse(text?.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value) ? value : 0;

        private static string Label(string target) => string.IsNullOrWhiteSpace(target) ? null : target.Trim().TrimStart(':');

        private static string StatName(string stat)
        {
            var key = StatsService.Definition(stat)?.Name;
            return string.IsNullOrEmpty(key) ? stat : Tr(key);
        }

        private static string Tr(string key) => TranslationServer.Translate(key).ToString();
    }
}
