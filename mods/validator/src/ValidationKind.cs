namespace Cthangover.Validator
{
    /// <summary>
    /// Entity kinds shared between JSON declaration collection and reference
    /// validation. A scenario strategy added later reuses these constants when
    /// it extracts background, avatar, quest and scene references from
    /// <c>.scenario</c> files, so both sides of the graph speak the same
    /// vocabulary.
    /// </summary>
    public static class ValidationKind
    {
        public const string Item = "item";
        public const string Quest = "quest";
        public const string Character = "character";
        public const string Action = "action";
        public const string StatusEffect = "status_effect";
        public const string Skill = "skill";
        public const string Recipe = "recipe";
        public const string Scene = "scene";
        public const string Interactive = "interactive";
        public const string Background = "background";
        public const string Avatar = "avatar";
        public const string Music = "music";
        public const string Sound = "sound";
        public const string Effect = "effect";
        public const string ScenePath = "scene_path";
    }
}
