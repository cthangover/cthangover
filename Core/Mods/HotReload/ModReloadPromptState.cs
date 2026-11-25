namespace Cthangover.Core.Mods.HotReload
{
    using System;

    /// <summary>Offers a manual action for two minutes; time passing never grants permission to reload.</summary>
    public sealed class ModReloadPromptState
    {
        public const double LifetimeSeconds = 120;
        private double expiresAt = double.NegativeInfinity;

        /// <summary>A fresh batch starts a new visibility window, including after an ignored offer.</summary>
        public void Offer(double nowSeconds)
        {
            if (!double.IsFinite(nowSeconds)) throw new ArgumentOutOfRangeException(nameof(nowSeconds));
            expiresAt = nowSeconds + LifetimeSeconds;
        }

        /// <summary>Checks the offer against monotonic real time, independent of game pause and simulation speed.</summary>
        public bool IsVisible(double nowSeconds) => nowSeconds < expiresAt;

        /// <summary>Consumes a still-visible offer exactly once when the user clicks.</summary>
        public bool TryConsume(double nowSeconds)
        {
            var available = IsVisible(nowSeconds);
            Dismiss();
            return available;
        }

        /// <summary>Hides the pending action without executing it, including for a manual keyboard reload.</summary>
        public void Dismiss() => expiresAt = double.NegativeInfinity;
    }
}
