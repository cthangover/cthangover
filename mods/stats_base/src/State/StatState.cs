namespace Cthangover.Mods.Stats
{
    /// <summary>
    /// The running game's <see cref="StatStateData"/>. Replaced on a new game and on a
    /// load (<see cref="StatsReset"/>, <see cref="StatsSaveable"/>); ids are stored as
    /// written, lookups ignore case.
    /// </summary>
    public class StatState
    {
        /// <summary>Shared instance.</summary>
        public static StatState Instance { get; } = new();

        /// <summary>The saved data.</summary>
        public StatStateData Data { get; set; } = new();

        /// <summary>The state of <paramref name="characterId"/>, or null when it gained nothing yet.</summary>
        public CharacterStatState Find(string characterId)
        {
            if (characterId == null)
                return null;
            if (Data.Characters.TryGetValue(characterId, out var state))
                return state;
            foreach (var pair in Data.Characters)
            {
                if (string.Equals(pair.Key, characterId, System.StringComparison.OrdinalIgnoreCase))
                    return pair.Value;
            }
            return null;
        }

        /// <summary>The state of <paramref name="characterId"/>, created when missing.</summary>
        public CharacterStatState Of(string characterId)
        {
            var state = Find(characterId);
            if (state != null)
                return state;
            state = new CharacterStatState();
            Data.Characters[characterId] = state;
            return state;
        }

        /// <summary>Back to a new game's empty state.</summary>
        public void Reset()
        {
            Data = new StatStateData();
            StatsService.ClearCache();
        }
    }
}
