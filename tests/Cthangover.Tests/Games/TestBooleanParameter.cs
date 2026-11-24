namespace Engine.Tests.Games
{
    using Cthangover.Core.Games.LaunchParameters;

    /// <summary>Example extension that requires an explicit boolean.</summary>
    public sealed class TestBooleanParameter : BooleanLaunchParameter
    {
        public TestBooleanParameter() { }

        public TestBooleanParameter(string value) : base(value) { }

        public override string Key => "test";

        public override string Documentation => "Enables the test feature when explicitly set to true.";
    }
}
