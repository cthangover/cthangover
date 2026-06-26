using Cthangover.Core.Battle.Actions;
using Cthangover.Core.Characters;

namespace Cthangover.Core.Battle
{
    /// <summary>
    /// Pluggable battle engine contract. Each implementation is a
    /// self-contained battle rule set (turn order, AI, phases).
    /// Init receives the two character arrays and a context adapter;
    /// Start begins the loop. The ActionProvider property lets the
    /// core inject its own executor overrides via the hub.
    /// Cores are instantiated fresh per battle — they carry no
    /// persistent state between encounters.
    /// </summary>
    public interface IBattleCore
    {
        string Id { get; }

        IActionExecutorProvider ActionProvider { get; }

        void Init(Character[] playerChars, Character[] enemyChars, IBattleContext ctx);

        void Start();
    }
}
