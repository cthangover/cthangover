namespace Cthangover.Core.Battle.Actions
{
    public interface IBattleAction
    {
        void DoStart();
        bool DoAction();
        void DoEnd();
    }
}
