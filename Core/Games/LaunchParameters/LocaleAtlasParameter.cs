namespace Cthangover.Core.Games.LaunchParameters
{
    /// <summary>Builds the localization key catalogue and exits.</summary>
    public sealed class LocaleAtlasParameter : SwitchLaunchParameter
    {
        /// <summary>Creates a definition with the parameter's default value.</summary>
        public LocaleAtlasParameter() { }

        /// <summary>Parses the value supplied for this parameter.</summary>
        public LocaleAtlasParameter(string value) : base(value) { }

        /// <summary>Canonical argument key.</summary>
        public const string Name = "locale-atlas";

        /// <inheritdoc />
        public override string Key => Name;

        /// <inheritdoc />
        public override string Documentation => "Builds the localization key catalogue and exits.";

        internal override bool RequestsDevelopmentMode => true;
        internal override bool ExcludesAutomaticHotReload => true;
    }
}
