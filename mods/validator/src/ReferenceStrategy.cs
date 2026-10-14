namespace Cthangover.Validator
{
    /// <summary>
    /// Phase: Reconciliation. Matches every reference collected by contract
    /// strategies against the declarations in the graph and reports
    /// <c>REF_NOT_FOUND</c> for the missing ones. Because the graph is global,
    /// a reference in one mod may resolve to a declaration in another — exactly
    /// how the runtime factories and scene loader resolve content.
    /// </summary>
    public class ReferenceStrategy : IValidationStrategy
    {
        public string Id => "references";
        public ValidationPhase Phase => ValidationPhase.Reconciliation;
        public int Order => 200;
        public ValidationFeature Feature => ValidationFeature.Json;

        /// <summary>Reports every recorded reference whose target id has no declaration in the merged fact graph (REF_NOT_FOUND).</summary>
        public void Validate(ValidationContext context)
        {
            foreach (var reference in context.Graph.References)
            {
                context.ThrowIfCancellationRequested();

                if (context.Graph.HasDeclaration(reference.Kind, reference.TargetId))
                    continue;

                context.AddError("REF_NOT_FOUND",
                    $"{reference.Kind} '{reference.TargetId}' is referenced but never declared",
                    reference.ModId, reference.File, reference.Line,
                    $"declare {reference.Kind} '{reference.TargetId}' or fix the reference");
            }
        }
    }
}
