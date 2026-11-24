namespace Cthangover.Core.Games.LaunchParameters
{
    /// <summary>A bare argument whose presence sets its value to true.</summary>
    public abstract class SwitchLaunchParameter : LaunchParameter<bool>
    {
        /// <summary>Creates an absent switch with a default value of false.</summary>
        protected SwitchLaunchParameter() { }

        /// <summary>Accepts a bare switch. A supplied value is invalid for this parameter.</summary>
        protected SwitchLaunchParameter(string raw) : base(raw == null, raw == null) { }

        /// <summary>Allows a derived switch to define another accepted form.</summary>
        protected SwitchLaunchParameter(bool value, bool isSpecified) : base(value, isSpecified) { }

        /// <inheritdoc />
        public override string Format => "flag";
    }
}
