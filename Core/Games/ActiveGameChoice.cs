namespace Cthangover.Core.Games
{
    /// <summary>
    /// The outcome of filtering a mod set for a launch selection — which game
    /// (or the front-end) it is — kept apart from <see cref="ActiveGame"/> so
    /// a mod-set reload can compute the new set, compile it and only then
    /// switch the process over. A failed reload (compile error) never applies
    /// its choice and the running game stays active.
    /// </summary>
    public sealed class ActiveGameChoice
    {
        /// <summary>No game: every mod stays (<c>--all-mods</c>, development launches) or the selection is unknown.</summary>
        public static readonly ActiveGameChoice None = new(null, frontend: false);

        /// <summary>A game's set (<paramref name="frontend"/> = false) or the front-end set; a null set means "no game".</summary>
        public ActiveGameChoice(GameModSet set, bool frontend)
        {
            Set = set;
            Frontend = frontend;
        }

        /// <summary>The resolved mod set of the chosen game or front-end; null when every mod stays.</summary>
        public GameModSet Set { get; }

        /// <summary>True when <see cref="Set"/> is the front-end only (no game chosen yet).</summary>
        public bool Frontend { get; }

        /// <summary>Makes this choice the process-wide <see cref="ActiveGame"/>.</summary>
        public void Apply()
        {
            if (Set == null)
                ActiveGame.Clear();
            else if (Frontend)
                ActiveGame.ActivateFrontend(Set);
            else
                ActiveGame.Activate(Set);
        }
    }
}
