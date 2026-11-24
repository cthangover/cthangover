namespace Cthangover.Core.Games.LaunchParameters
{
    /// <summary>Opens a logical mod scene after launcher boot.</summary>
    public sealed class SceneParameter : StringLaunchParameter
    {
        /// <summary>Creates a definition with the parameter's default value.</summary>
        public SceneParameter() { }

        /// <summary>Parses the value supplied for this parameter.</summary>
        public SceneParameter(string value) : base(value?.Trim()) { }

        /// <summary>Canonical argument key.</summary>
        public const string Name = "scene";

        /// <inheritdoc />
        public override string Key => Name;

        /// <inheritdoc />
        public override string Documentation => "Opens a logical mod scene after launcher boot.";

        internal override bool RequestsDevelopmentMode => true;
    }
}
