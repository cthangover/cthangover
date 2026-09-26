namespace Cthangover.Validator
{
    /// <summary>
    /// Ordered stages of a validation pass. Strategies declare the stage they
    /// belong to, which lets later strategies consume facts collected by earlier
    /// ones: schema checks need nothing, contract collectors (JSON graph, C#
    /// usages) produce the facts, and reconciliation strategies compare the
    /// collected declarations, references and usages against each other.
    ///
    /// A scenario-validation strategy added later normally belongs to Contracts
    /// (extract declarations and references from <c>.scenario</c> files) and
    /// Reconciliation (match them against the graph).
    /// </summary>
    public enum ValidationPhase
    {
        /// <summary>Shape of source files: manifest fields, JSON syntax.</summary>
        Schema = 0,

        /// <summary>Fact collection: entity declarations, references, code usages.</summary>
        Contracts = 1,

        /// <summary>Cross-checks: missing references, conflicts, unused declarations.</summary>
        Reconciliation = 2,
    }
}
