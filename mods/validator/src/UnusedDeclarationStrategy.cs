using System;
using System.Collections.Generic;

namespace Cthangover.Validator
{
    /// <summary>
    /// Phase: Reconciliation. Reports declarations that no static reference
    /// points at. Coverage is deliberately limited to kinds whose references
    /// are fully collected today — items (loot, recipe ingredients) and actions
    /// (character action lists). Backgrounds, avatars and scenes are consumed
    /// mostly from <c>.scenario</c> files, so they stay out until a scenario
    /// strategy contributes those references; otherwise every asset would be
    /// flagged.
    /// </summary>
    public class UnusedDeclarationStrategy : IValidationStrategy
    {
        public string Id => "unused_declarations";
        public ValidationPhase Phase => ValidationPhase.Reconciliation;
        public int Order => 300;
        public ValidationFeature Feature => ValidationFeature.Json;

        /// <summary>Reports items and actions that are declared but never referenced (DECL_UNUSED).</summary>
        public void Validate(ValidationContext context)
        {
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var reference in context.Graph.References)
                used.Add(Key(reference.Kind, reference.TargetId));

            foreach (var declaration in context.Graph.Declarations)
            {
                context.ThrowIfCancellationRequested();

                if (declaration.Kind != ValidationKind.Item && declaration.Kind != ValidationKind.Action)
                    continue;

                if (used.Contains(Key(declaration.Kind, declaration.Id)))
                    continue;

                context.AddInfo("DECL_UNUSED",
                    $"{declaration.Kind} '{declaration.Id}' is never referenced statically",
                    declaration.ModId, declaration.File, declaration.Line);
            }
        }

        private static string Key(string kind, string id) => kind + "|" + id;
    }
}
