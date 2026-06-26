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
        public string Name => "character.add_to_party";

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
