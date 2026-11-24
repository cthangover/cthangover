namespace Cthangover.Core.Scenes
{
    using Cthangover.Core.Games;
    using Cthangover.Core.Games.LaunchParameters;

    /// <summary>Provides the logical scene selection to existing launcher and test callers.</summary>
    public static class SceneLaunchArgs
    {
        /// <summary>The first valid logical scene specified in the effective launch arguments.</summary>
        public static string RequestedSceneName() => GameLaunchArgs.Current.Get<SceneParameter>().Value;
    }
}
