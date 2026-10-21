namespace Cthangover.Core.Characters
{
    /// <summary>
    /// Cooldown helpers for <see cref="ActionCharacterType.Active"/> cards.
    /// Active cards are one-shot abilities usable outside battle (from the
    /// character panel). The last-use timestamp is stored in
    /// <see cref="ActionCharacter.Properties"/> under
    /// 'LastUsed' - the cooldown duration
    /// (seconds) under 'ActiveCooldown'
    /// Absence of <c>LastUsed</c> means the card has never been activated.
    /// </summary>
    public static class ActionCharacterActiveExtension
    {
        /// <summary>
        /// Returns <c>true</c> when the card is <see cref="ActionCharacterType.Active"/>
        /// and its cooldown has expired (or it was never used).
        /// </summary>
        public static bool IsReadyToUse(this ActionCharacter action, long currentTime)
        {
            if (action.Type != ActionCharacterType.Active)
                return false;

            var lastUsed = action.Properties.GetLong("LastUsed", 0);
            var cooldown = action.GetInt("ActiveCooldown", 0);

            if (lastUsed == 0)
                return true;
            if (cooldown <= 0)
                return false;

            return currentTime - lastUsed >= cooldown;
        }

        /// <summary>
        /// Seconds remaining until the cooldown expires. Returns <c>0</c> when
        /// ready, never used, or not an Active card.
        /// </summary>
        public static long GetRemainingCooldown(this ActionCharacter action, long currentTime)
        {
            if (action.Type != ActionCharacterType.Active)
                return 0;

            var lastUsed = action.Properties.GetLong("LastUsed", 0);
            var cooldown = action.GetInt("ActiveCooldown", 0);

            if (lastUsed == 0 || cooldown <= 0)
                return 0;

            var elapsed = currentTime - lastUsed;
            return elapsed >= cooldown ? 0 : cooldown - elapsed;
        }

        /// <summary>
        /// Records the activation timestamp. Has no effect on non-Active cards.
        /// </summary>
        public static void MarkUsed(this ActionCharacter action, long timestamp)
        {
            if (action.Type != ActionCharacterType.Active)
                return;
            action.Properties.SetLong("LastUsed", timestamp);
        }
    }
}
