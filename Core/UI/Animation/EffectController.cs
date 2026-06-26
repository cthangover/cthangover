namespace Cthangover.Core.UI.Animation
{
    /// <summary>
    /// Specialisation of AnimationController where Pause() also calls Clear(),
    /// treating "pause" as a hard reset. This is intentional for one-shot effects
    /// that should clean up instantly rather than freezing on their last frame.
    /// The "new" keyword on Pause() shadows the base implementation on purpose.
    /// </summary>
    public partial class EffectController : AnimationController
    {
        public new void Pause()
        {
            Clear();
        }
    }
}
