namespace Cthangover.Core.Actions.Atomic
{
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
