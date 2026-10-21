using Cthangover.Core.Actions;

namespace Cthangover.Mods.Characters
{
    /// <summary>
    /// <c>action party.add id=hero</c> — puts a character template into the
    /// player's party and battle set. Lets a game without C# give the player
    /// their starting party from its first scenario (a new game resets the
    /// party and replays that scenario). A character already in the roster keeps
    /// their level and health and only returns to the battle set.
    /// </summary>
    public class PartyAddAction : IScenarioAction
    {
        /// <summary>DSL name.</summary>
        public string Name => "party.add";

        /// <summary>Reads <c>id</c> and adds the character.</summary>
        public void Run(IActionContext ctx)
        {
            var id = ctx.GetParam("id");
            if (string.IsNullOrWhiteSpace(id))
            {
                ctx.LogWarning("EVENT", "party.add: missing 'id'");
                return;
            }
            ctx.Character.AddToParty(id.Trim());
        }
    }
}
