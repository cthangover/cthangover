namespace Cthangover.Core.Actions.Atomic
{
    /// <summary>
    /// Recruits a character to the player's party by CharacterType enum name.
    /// The "type" variable should match a CharacterType value exactly (case-
    /// sensitive parsing via Enums&lt;CharacterType&gt;.Parse). Delegates to
    /// CharacterData.AddCharacterToParty which handles the actual roster mutation.
    /// </summary>
    public class CharacterAddToPartyAction : IScenarioAction
    {
        /// <summary>
        /// Registered as "character.add_to_party" — adds a character to
        /// the player's party roster. The "type" dialog variable must match
        /// a CharacterType enum value exactly (case-sensitive). Delegates
        /// to CharacterData.AddCharacterToParty which handles persistence.
        /// Invalid type strings throw at parse time — this surfaces typos
        /// during scenario testing rather than silently failing.
        /// </summary>
        public string Name => "character.add_to_party";

        /// <summary>
        /// Reads the "type" variable and recruits the character to the
        /// party. Returns early with a log warning if "type" is missing
        /// or empty. The character is immediately available in the party
        /// roster after this call returns.
        /// </summary>
        public void Run(IActionContext ctx)
        {
            var typeRaw = ctx.GetParam("type");

            if (string.IsNullOrEmpty(typeRaw))
            {
                ctx.Log("EVENT", "CharacterAddToPartyAction: missing 'type' variable");
                return;
            }

            ctx.Character.AddToParty(typeRaw);
            ctx.Log("EVENT", $"CharacterAddToPartyAction: added '{typeRaw}' to party");
        }
    }
}
