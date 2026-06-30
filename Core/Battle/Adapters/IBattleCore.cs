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
        /// <summary>
        /// Unique identifier for this battle engine (e.g. "card_battle",
        /// "ff_battle"). Used by <see cref="BattleCoreRegistry"/> and
        /// <see cref="BattleData.ActiveBattleCore"/> for lookup.
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Provider that returns core-specific <see cref="IActionExecutor"/>
        /// overrides. Set on the <see cref="ActionExecutorHub"/> when
        /// this core activates, so actions route through custom logic
        /// before falling back to the global registry.
        /// </summary>
        IActionExecutorProvider ActionProvider { get; }

        /// <summary>
        /// Initialises the core with the two character arrays and a
        /// <see cref="IBattleContext"/> for callbacks. Called once per
        /// battle before <see cref="Start"/>.
        /// </summary>
        void Init(Character[] playerChars, Character[] enemyChars, IBattleContext ctx);

        /// <summary>
        /// Begins the turn loop. Cores may use coroutines, polling, or
        /// event-driven state machines — the core owns its own lifecycle
        /// after this call.
        /// </summary>
        void Start();
    }
}
