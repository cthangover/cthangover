namespace Cthangover.Validator
{
    /// <summary>
    /// One self-contained validation step, discovered by reflection like every
    /// other plugin point in the project (<c>Reflections.FindAndCreate</c>).
    /// Strategies run sequentially in (Phase, Order, Id) order on the validator
    /// background thread: Schema strategies validate source shapes, Contracts
    /// strategies fill <see cref="ValidationContext.Graph"/> with declarations,
    /// references and code usages, and Reconciliation strategies compare those
    /// facts and emit findings.
    ///
    /// To add scenario validation, implement this interface in a new class:
    /// extract declarations and references into the graph in the Contracts
    /// phase and match them in Reconciliation — no service changes are needed.
    /// </summary>
    public interface IValidationStrategy
    {
        /// <summary>Stable identifier used in logs and in the sort order.</summary>
        string Id { get; }

        /// <summary>Execution stage; strategies of earlier phases run first.</summary>
        ValidationPhase Phase { get; }

        /// <summary>Order inside the phase; lower runs first.</summary>
        int Order { get; }

        /// <summary>Capability group gated by the user settings.</summary>
        ValidationFeature Feature { get; }

        /// <summary>
        /// Runs the strategy. Data problems must be reported through the
        /// <see cref="ValidationContext"/> and must not throw; unexpected
        /// exceptions are caught and logged by <see cref="ValidatorService"/>.
        /// </summary>
        void Validate(ValidationContext context);
    }
}
