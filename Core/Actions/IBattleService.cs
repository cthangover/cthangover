namespace Cthangover.Core.Actions
{
    public interface IBattleService
    {
        void Init(string sceneType, string enemies, string questId = null, string newTag = null);
    }
}