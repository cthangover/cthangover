using Cthangover.Core.Actions;

namespace Engine.Tests.Actions;

internal class MockBattleService : IBattleService
{
    public string LastSceneType { get; private set; }
    public string LastEnemies { get; private set; }
    public string LastQuestId { get; private set; }
    public string LastNewTag { get; private set; }

    public void Init(string sceneType, string enemies, string questId = null, string newTag = null)
    {
        LastSceneType = sceneType;
        LastEnemies = enemies;
        LastQuestId = questId;
        LastNewTag = newTag;
    }
}
