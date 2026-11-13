namespace Cthangover.Tests.Testing
{
    /// <summary>Resource-owning runner probes must not overlap engine tests that sweep static roots or wait for async continuations.</summary>
    [CollectionDefinition("runner-infrastructure", DisableParallelization = true)]
    public sealed class RunnerInfrastructureCollection { }
}
