using Cthangover.Core.Battle.Actions;
using Cthangover.Core.Characters;

namespace Cthangover.Core.Battle
{
    public interface IBattleCore
    {
        string Id { get; }

        IActionExecutorProvider ActionProvider { get; }

        void Init(Character[] playerChars, Character[] enemyChars, IBattleContext ctx);

        void Start();
    }
}
