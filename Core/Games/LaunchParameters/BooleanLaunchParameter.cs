namespace Cthangover.Core.Games.LaunchParameters
{
    /// <summary>A parameter with an explicit true or false value after the equals sign.</summary>
    public abstract class BooleanLaunchParameter : LaunchParameter<bool>
    {
        /// <summary>Creates an absent argument with a default value of false.</summary>
        protected BooleanLaunchParameter() { }

        /// <summary>Parses an explicit boolean.</summary>
        protected BooleanLaunchParameter(string raw) : this(Parse(raw)) { }

        private BooleanLaunchParameter((bool Value, bool Valid) parsed) : base(parsed.Value, parsed.Valid) { }

        private static (bool Value, bool Valid) Parse(string raw)
        {
            var valid = bool.TryParse(raw, out var value);
            return (value, valid);
        }

        /// <inheritdoc />
        public override string Format => "boolean";
    }
}
