namespace Cthangover.Core.Games.LaunchParameters
{
    using System;

    /// <summary>A comma-separated argument. The returned array is a detached copy.</summary>
    public abstract class StringListLaunchParameter : LaunchParameter<string[]>
    {
        /// <summary>Creates an absent argument with an empty list.</summary>
        protected StringListLaunchParameter() : base(Array.Empty<string>()) { }

        /// <summary>Splits the argument and trims its items.</summary>
        protected StringListLaunchParameter(string raw)
            : base(raw?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? Array.Empty<string>(),
                   !string.IsNullOrWhiteSpace(raw)) { }

        /// <inheritdoc />
        public override string Format => "string[]";

        /// <inheritdoc />
        protected override string[] CopyValue(string[] parsed) => (string[])parsed.Clone();
    }
}
