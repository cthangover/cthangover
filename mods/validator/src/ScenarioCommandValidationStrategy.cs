using System;
using Cthangover.Mods.Dialog.Scenarios;
using System.Collections.Generic;
using Cthangover.Core.Factories.Impls;
using Cthangover.Core.Mods;
using Cthangover.Core.Scenarios;
using Cthangover.Core.Scenes;

namespace Cthangover.Validator
{
    /// <summary>
    /// Phase: Contracts. Validates <c>.scenario</c> DSL files. The command
    /// vocabulary is not hardcoded: known command words come from
    /// <see cref="ScenarioCommandStrategyFactory"/> (mods add commands through
    /// <c>RegisterAssembly</c>), and each command's argument contract comes from
    /// <see cref="IScenarioCommandSpec"/> implemented by the strategy itself.
    /// Commands without a spec are still recognised, just not argument-checked.
    ///
    /// Checks performed:
    /// <list type="bullet">
    /// <item>unknown command words (<c>SCENARIO_UNKNOWN_COMMAND</c>);</item>
    /// <item>missing / extra / unknown / empty arguments based on the command spec;</item>
    /// <item>jump targets (<c>goto</c>, <c>option</c>) must point at an existing <c>:label</c>;</item>
    /// <item><c>select</c> without options and <c>option</c> outside a select block;</item>
    /// <item>references collected by <see cref="ICommandReferenceMetadata"/> are added to the
    /// shared graph (backgrounds, scenes, music, sounds, effects, avatars) and checked by
    /// <see cref="ReferenceStrategy"/>; scenario actions are checked against the action registry.</item>
    /// <item>content references inside action arguments (<see cref="ActionArgumentReferences"/>:
    /// character ids, skills, quest ids, battle enemy lists) are added to the same graph, so a
    /// typo is reported against the declaring mods with file and line.</item>
    /// </list>
    /// </summary>
    public class ScenarioCommandValidationStrategy : IValidationStrategy
    {
        private static readonly string[] SelectAllowedNamed = { "key", "first", "second" };
        private static readonly string[] OptionAllowedNamed = { "key", "goto" };

        private static readonly object actionLock = new();
        private static HashSet<string> actionNames;

        public string Id => "scenario_commands";
        public ValidationPhase Phase => ValidationPhase.Contracts;
        public int Order => 300;
        public ValidationFeature Feature => ValidationFeature.Scenarios;

        /// <summary>Parses every .scenario file and validates DSL commands, named parameters and known action names.</summary>
        public void Validate(ValidationContext context)
        {
            foreach (var mod in context.Mods)
            {
                context.ThrowIfCancellationRequested();

                var provider = mod.FileProvider;
                if (provider == null)
                    continue;

                foreach (var file in ModFileReader.ListFiles(provider, "scenarios", true))
                {
                    context.ThrowIfCancellationRequested();
                    if (!file.EndsWith(".scenario", StringComparison.OrdinalIgnoreCase))
                        continue;

                    ValidateFile(context, mod, file);
                }
            }
        }

        private static void ValidateFile(ValidationContext context, IModInfo mod, string file)
        {
            string text;
            try
            {
                text = ModFileReader.ReadResolved(mod.FileProvider, file);
            }
            catch (Exception ex)
            {
                context.AddError("SCENARIO_PARSE_ERROR", $"cannot read scenario: {ex.Message}", mod.Id, file);
                return;
            }

            if (text == null)
                return;

            var metadataResult = ScenarioParser.ParseMetadata(text);
            var body = metadataResult.remainingText;
            var lines = body.Split('\n');
            var lineOffset = text.Split('\n').Length - lines.Length;

            var labels = CollectLabels(context, mod, file, lines, lineOffset);

            var pendingSelectLine = -1;
            var pendingSelectHasOptions = false;
            var lineNumber = lineOffset;

            foreach (var rawLine in lines)
            {
                lineNumber++;
                var line = rawLine.Trim();

                if (line.Length == 0 || line.StartsWith('#'))
                {
                    FinalizeSelect(context, mod, file, ref pendingSelectLine, ref pendingSelectHasOptions);
                    continue;
                }

                if (line.StartsWith(':'))
                {
                    FinalizeSelect(context, mod, file, ref pendingSelectLine, ref pendingSelectHasOptions);
                    continue;
                }

                var tokens = Tokenize(line);
                if (tokens.Count == 0)
                    continue;

                var command = tokens[0].ToLowerInvariant();
                tokens.RemoveAt(0);

                var positional = new List<string>();
                var named = new Dictionary<string, string>();
                SplitArguments(tokens, positional, named, out var arrowTarget);

                if (command == "select")
                {
                    FinalizeSelect(context, mod, file, ref pendingSelectLine, ref pendingSelectHasOptions);
                    pendingSelectLine = lineNumber;
                    pendingSelectHasOptions = false;

                    CheckEmptyNamed(context, mod, file, lineNumber, command, named);
                    CheckAllowedNamed(context, mod, file, lineNumber, command, named, SelectAllowedNamed);
                    CheckPositionalRange(context, mod, file, lineNumber, command, positional, 0, 1);
                    AddAvatarReferences(context, mod, file, lineNumber, named);
                    continue;
                }

                if (command == "option")
                {
                    if (pendingSelectLine < 0)
                        context.AddWarning("SCENARIO_ORPHAN_OPTION",
                            "option outside a select block is ignored by the parser",
                            mod.Id, file, lineNumber);
                    else
                        pendingSelectHasOptions = true;

                    CheckEmptyNamed(context, mod, file, lineNumber, command, named);
                    CheckAllowedNamed(context, mod, file, lineNumber, command, named, OptionAllowedNamed);

                    var target = arrowTarget;
                    if (string.IsNullOrEmpty(target) && named.TryGetValue("goto", out var goTo))
                        target = goTo;
                    else if (string.IsNullOrEmpty(target) && positional.Count > 1 && positional[1].StartsWith(':'))
                        target = positional[1].TrimStart(':');

                    CheckLabel(context, mod, file, lineNumber, command, target, labels);
                    continue;
                }

                FinalizeSelect(context, mod, file, ref pendingSelectLine, ref pendingSelectHasOptions);

                var strategy = ScenarioCommandStrategyFactory.Get(command);
                if (strategy == null)
                {
                    context.AddError("SCENARIO_UNKNOWN_COMMAND",
                        $"unknown scenario command '{command}'",
                        mod.Id, file, lineNumber,
                        "check the command name or make sure the mod that defines it is enabled");
                    continue;
                }

                var spec = strategy as IScenarioCommandSpec;
                if (spec != null)
                    CheckSpec(context, mod, file, lineNumber, command, positional, named, arrowTarget, spec, labels);

                CheckEmptyNamed(context, mod, file, lineNumber, command, named);
                AddAvatarReferences(context, mod, file, lineNumber, named);

                if (!string.IsNullOrEmpty(arrowTarget))
                    CheckLabel(context, mod, file, lineNumber, command, arrowTarget, labels);

                var referenceMetadata = strategy as ICommandReferenceMetadata;
                if (referenceMetadata != null
                    && referenceMetadata.Positional0Kind != PositionalReferenceKind.None
                    && positional.Count > 0)
                {
                    if (string.IsNullOrWhiteSpace(positional[0]))
                        context.AddError("SCENARIO_EMPTY_ARGUMENT",
                            $"'{command}' requires a non-empty {referenceMetadata.Positional0Kind} reference",
                            mod.Id, file, lineNumber);
                    else
                        AddReference(context, mod, file, lineNumber, referenceMetadata.Positional0Kind, positional[0]);
                }

                if (command == "action" && positional.Count > 0)
                    AddActionArgumentReferences(context, mod, file, lineNumber, positional[0], named);
            }

            FinalizeSelect(context, mod, file, ref pendingSelectLine, ref pendingSelectHasOptions);
        }

        private static HashSet<string> CollectLabels(ValidationContext context, IModInfo mod, string file, string[] lines, int lineOffset)
        {
            var labels = new HashSet<string>(StringComparer.Ordinal);
            var lineNumber = lineOffset;

            foreach (var rawLine in lines)
            {
                lineNumber++;
                var line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith('#'))
                    continue;

                if (!line.StartsWith(':'))
                    continue;

                var label = line.Substring(1).Trim();
                if (label.Length == 0)
                {
                    context.AddError("SCENARIO_MISSING_ARGUMENT", "label name is empty", mod.Id, file, lineNumber);
                    continue;
                }

                if (!labels.Add(label))
                    context.AddWarning("SCENARIO_DUPLICATE_LABEL", $"label ':{label}' is defined more than once", mod.Id, file, lineNumber);
            }

            return labels;
        }

        private static void FinalizeSelect(ValidationContext context, IModInfo mod, string file, ref int pendingSelectLine, ref bool pendingSelectHasOptions)
        {
            if (pendingSelectLine >= 0 && !pendingSelectHasOptions)
                context.AddWarning("SCENARIO_EMPTY_SELECT",
                    "select is not followed by any option and is dropped from the queue",
                    mod.Id, file, pendingSelectLine);

            pendingSelectLine = -1;
            pendingSelectHasOptions = false;
        }

        private static void CheckSpec(
            ValidationContext context,
            IModInfo mod,
            string file,
            int line,
            string command,
            List<string> positional,
            Dictionary<string, string> named,
            string arrowTarget,
            IScenarioCommandSpec spec,
            HashSet<string> labels)
        {
            var argumentCount = positional.Count + named.Count;
            if (argumentCount == 0 && !string.IsNullOrEmpty(arrowTarget))
                argumentCount = 1;

            if (positional.Count < spec.MinPositional)
                context.AddError("SCENARIO_MISSING_ARGUMENT",
                    $"'{command}' requires at least {spec.MinPositional} positional argument(s), got {positional.Count}",
                    mod.Id, file, line);

            if (argumentCount < spec.MinArguments)
                context.AddError("SCENARIO_MISSING_ARGUMENT",
                    $"'{command}' requires at least {spec.MinArguments} argument(s), got {argumentCount}",
                    mod.Id, file, line);

            if (spec.MaxPositional >= 0 && positional.Count > spec.MaxPositional)
                context.AddWarning("SCENARIO_UNEXPECTED_ARGUMENT",
                    $"'{command}' accepts at most {spec.MaxPositional} positional argument(s), got {positional.Count}",
                    mod.Id, file, line);

            CheckPositionalRange(context, mod, file, line, command, positional, spec.MinPositional, spec.MaxPositional);

            var required = spec.RequiredNamed;
            if (required != null)
            {
                foreach (var name in required)
                {
                    if (!named.TryGetValue(name.ToLowerInvariant(), out var value) || string.IsNullOrWhiteSpace(value))
                        context.AddError("SCENARIO_MISSING_ARGUMENT",
                            $"'{command}' requires named argument '{name}'",
                            mod.Id, file, line);
                }
            }

            var allowed = spec.AllowedNamed;
            if (allowed != null && allowed.Count > 0)
                CheckAllowedNamed(context, mod, file, line, command, named, allowed);

            var jumpKey = spec.JumpTargetNamedKey;
            if (!string.IsNullOrEmpty(jumpKey) && named.TryGetValue(jumpKey, out var jumpTarget))
                CheckLabel(context, mod, file, line, command, jumpTarget, labels);
        }

        private static void CheckPositionalRange(
            ValidationContext context,
            IModInfo mod,
            string file,
            int line,
            string command,
            List<string> positional,
            int min,
            int max)
        {
            if (min < 1 || positional.Count == 0)
                return;

            for (var i = 0; i < positional.Count && i < min; i++)
            {
                if (string.IsNullOrEmpty(positional[i]))
                    context.AddError("SCENARIO_EMPTY_ARGUMENT",
                        $"'{command}' positional argument #{i + 1} is empty",
                        mod.Id, file, line);
            }
        }

        private static void CheckEmptyNamed(
            ValidationContext context,
            IModInfo mod,
            string file,
            int line,
            string command,
            Dictionary<string, string> named)
        {
            foreach (var pair in named)
            {
                if (string.IsNullOrEmpty(pair.Value))
                    context.AddError("SCENARIO_EMPTY_ARGUMENT",
                        $"argument '{pair.Key}' of '{command}' has no value",
                        mod.Id, file, line);
            }
        }

        private static void CheckAllowedNamed(
            ValidationContext context,
            IModInfo mod,
            string file,
            int line,
            string command,
            Dictionary<string, string> named,
            IReadOnlyList<string> allowed)
        {
            foreach (var pair in named)
            {
                if (!ContainsName(allowed, pair.Key))
                    context.AddWarning("SCENARIO_UNKNOWN_ARGUMENT",
                        $"'{command}' does not accept argument '{pair.Key}'",
                        mod.Id, file, line);
            }
        }

        private static bool ContainsName(IReadOnlyList<string> names, string name)
        {
            for (var i = 0; i < names.Count; i++)
            {
                if (string.Equals(names[i], name, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static void CheckLabel(
            ValidationContext context,
            IModInfo mod,
            string file,
            int line,
            string command,
            string target,
            HashSet<string> labels)
        {
            if (string.IsNullOrEmpty(target) || labels.Contains(target))
                return;

            context.AddError("SCENARIO_MISSING_LABEL",
                $"'{command}' jumps to ':{target}' but no such label exists in the file",
                mod.Id, file, line,
                "add the label or fix the jump target");
        }

        private static void AddAvatarReferences(ValidationContext context, IModInfo mod, string file, int line, Dictionary<string, string> named)
        {
            AddAvatarReference(context, mod, file, line, named, "first");
            AddAvatarReference(context, mod, file, line, named, "second");
        }

        private static void AddAvatarReference(ValidationContext context, IModInfo mod, string file, int line, Dictionary<string, string> named, string name)
        {
            if (!named.TryGetValue(name, out var value) || string.IsNullOrWhiteSpace(value))
                return;

            context.Graph.AddReference(ValidationKind.Avatar, value, mod.Id, file, line);
        }

        /// <summary>
        /// Collects content references hidden in the named arguments of an
        /// <c>action</c> command. The scenario DSL treats action arguments as
        /// opaque dialog variables, so without this pass a typo like
        /// <c>action character.add_to_party type=Murakamiy</c> would only surface
        /// at runtime as a factory error log. Values from
        /// <see cref="ActionArgumentReferences"/> become graph references and are
        /// reconciled by <see cref="ReferenceStrategy"/>, which reports the
        /// offending scenario file and line. Comma-separated values (e.g.
        /// battle.init enemies=) produce one reference per entry; values with the
        /// ${name} marker are computed at play time and cannot be resolved
        /// statically, so they are skipped instead of misreported.
        /// </summary>
        private static void AddActionArgumentReferences(
            ValidationContext context,
            IModInfo mod,
            string file,
            int line,
            string actionName,
            Dictionary<string, string> named)
        {
            if (!ActionArgumentReferences.TryGetKinds(actionName, out var kinds))
                return;

            foreach (var pair in kinds)
            {
                if (!named.TryGetValue(pair.Key, out var value) || string.IsNullOrWhiteSpace(value))
                    continue;

                if (value.Contains("$" + "{"))
                    continue;

                foreach (var part in value.Split(','))
                {
                    var target = part.Trim();
                    if (target.Length == 0)
                        continue;

                    if (pair.Value == ValidationKind.Scene && SceneManager.IsNativeSceneName(target))
                        continue;

                    context.Graph.AddReference(pair.Value, target, mod.Id, file, line);
                }
            }
        }

        private static void AddReference(ValidationContext context, IModInfo mod, string file, int line, PositionalReferenceKind kind, string target)
        {
            switch (kind)
            {
                case PositionalReferenceKind.Background:
                    context.Graph.AddReference(ValidationKind.Background, target, mod.Id, file, line);
                    break;
                case PositionalReferenceKind.Scene:
                    if (!SceneManager.IsNativeSceneName(target))
                        context.Graph.AddReference(ValidationKind.Scene, target, mod.Id, file, line);
                    break;
                case PositionalReferenceKind.Music:
                    context.Graph.AddReference(ValidationKind.Music, target, mod.Id, file, line);
                    break;
                case PositionalReferenceKind.Sound:
                    context.Graph.AddReference(ValidationKind.Sound, target, mod.Id, file, line);
                    break;
                case PositionalReferenceKind.Effect:
                    context.Graph.AddReference(ValidationKind.Effect, target, mod.Id, file, line);
                    break;
                case PositionalReferenceKind.Action:
                    CheckAction(context, mod, file, line, target);
                    break;
            }
        }

        private static void CheckAction(ValidationContext context, IModInfo mod, string file, int line, string target)
        {
            if (GetActionNames().Contains(target))
                return;

            context.AddError("REF_NOT_FOUND",
                $"scenario action '{target}' is not registered",
                mod.Id, file, line,
                "check the action name or make sure the mod that defines it is enabled");
        }

        private static HashSet<string> GetActionNames()
        {
            lock (actionLock)
            {
                if (actionNames != null)
                    return actionNames;

                var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var action in ScenarioActionFactory.Instance.GetAll())
                    names.Add(action.Name);

                actionNames = names;
                return actionNames;
            }
        }

        private static void SplitArguments(List<string> tokens, List<string> positional, Dictionary<string, string> named, out string arrowTarget)
        {
            arrowTarget = string.Empty;

            for (var i = 0; i < tokens.Count; i++)
            {
                var token = tokens[i];

                if (token == "->")
                {
                    if (i + 1 < tokens.Count)
                    {
                        arrowTarget = tokens[i + 1].TrimStart(':');
                        i++;
                    }
                    continue;
                }

                var equalsIndex = token.IndexOf('=');
                if (equalsIndex > 0)
                {
                    var name = token.Substring(0, equalsIndex);
                    if (IsValidIdent(name))
                    {
                        named[name.ToLowerInvariant()] = token.Substring(equalsIndex + 1);
                        continue;
                    }
                }

                positional.Add(token);
            }
        }

        private static List<string> Tokenize(string line)
        {
            var result = new List<string>();
            var i = 0;

            while (i < line.Length)
            {
                if (char.IsWhiteSpace(line[i]))
                {
                    i++;
                    continue;
                }

                if (line[i] == '"')
                {
                    i++;
                    var start = i;
                    while (i < line.Length && line[i] != '"')
                    {
                        if (line[i] == '\\' && i + 1 < line.Length)
                            i++;
                        i++;
                    }
                    result.Add(line.Substring(start, i - start).Replace("\\\"", "\""));
                    if (i < line.Length)
                        i++;
                    continue;
                }

                var tokenStart = i;
                while (i < line.Length && !char.IsWhiteSpace(line[i]))
                    i++;
                result.Add(line.Substring(tokenStart, i - tokenStart));
            }

            return result;
        }

        private static bool IsValidIdent(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                if (!char.IsLetterOrDigit(c) && c != '_')
                    return false;
            }

            return true;
        }
    }
}
