namespace Engine.Tests.Mods
{
    /// <summary>Matches the compile error of a mod skipped because its dependency did not compile.</summary>
    public sealed class SkippedDependentMatcher
    {
        private readonly string dependency;

        public SkippedDependentMatcher(string dependency) => this.dependency = dependency;

        public bool Matches(string error) => error.Contains("dependency '" + dependency + "' did not compile");
    }
}
