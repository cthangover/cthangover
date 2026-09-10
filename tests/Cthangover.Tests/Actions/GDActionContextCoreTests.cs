#if false // Moved to integration tests: tests/Cthangover.Tests.Integration/Autotest/GDActionContextCoreTestSetup.cs
using Cthangover.Core.Actions;

namespace Engine.Tests.Actions;

public class GDActionContextCoreTests
{
    [Fact]
    public void GetParam_DelegatesToInner()
    {
        var mock = new MockActionContext();
        mock.SetParam("key1", "value1");
        mock.SetParam("key2", "");
        var core = new GDActionContextCore { Inner = mock };

        Assert.Equal("value1", core.GetParam("key1"));
        Assert.Equal("", core.GetParam("key2"));
        Assert.Null(core.GetParam("missing"));
    }

    [Fact]
    public void Log_DelegatesToInner()
    {
        var mock = new MockActionContext();
        var core = new GDActionContextCore { Inner = mock };

        core.Log("CAT", "msg");
        core.Log("EVENT", "another");

        Assert.Equal(2, mock.Logs.Count);
        Assert.Equal(("CAT", "msg"), mock.Logs[0]);
        Assert.Equal(("EVENT", "another"), mock.Logs[1]);
    }

    [Fact]
    public void LogWarning_DelegatesToInner()
    {
        var mock = new MockActionContext();
        var core = new GDActionContextCore { Inner = mock };

        core.LogWarning("WARN", "oops");

        Assert.Single(mock.Warnings);
        Assert.Equal(("WARN", "oops"), mock.Warnings[0]);
    }

    [Fact]
    public void QuestExists_DelegatesToInner()
    {
        var mock = new MockActionContext();
        var questSvc = new MockQuestService();
        questSvc.ExistsResult = true;
        mock.Quests = questSvc;
        var core = new GDActionContextCore { Inner = mock };

        Assert.True(core.Quests.Exists("q1"));

        questSvc.ExistsResult = false;
        Assert.False(core.Quests.Exists("q2"));
    }

    [Fact]
    public void QuestSetStatus_DelegatesToInner()
    {
        var mock = new MockActionContext();
        var questSvc = new MockQuestService();
        mock.Quests = questSvc;
        var core = new GDActionContextCore { Inner = mock };

        core.Quests.SetStatus("quest_1", "Active");

        Assert.Equal("quest_1", questSvc.LastSetStatusId);
        Assert.Equal("Active", questSvc.LastSetStatusStatus);
    }

    [Fact]
    public void QuestSetDataStatus_DelegatesToInner()
    {
        var mock = new MockActionContext();
        var questSvc = new MockQuestService();
        mock.Quests = questSvc;
        var core = new GDActionContextCore { Inner = mock };

        core.Quests.SetDataStatus("q", 5);

        Assert.Equal("q", questSvc.LastDataStatusId);
        Assert.Equal(5, questSvc.LastDataStatusLevel);
    }

    [Fact]
    public void QuestAddTag_DelegatesToInner()
    {
        var mock = new MockActionContext();
        var questSvc = new MockQuestService();
        mock.Quests = questSvc;
        var core = new GDActionContextCore { Inner = mock };

        core.Quests.AddTag("q", "found_key");

        Assert.Equal("q", questSvc.LastAddedTagId);
        Assert.Equal("found_key", questSvc.LastAddedTag);
    }

    [Fact]
    public void QuestRemoveTag_DelegatesToInner()
    {
        var mock = new MockActionContext();
        var questSvc = new MockQuestService();
        mock.Quests = questSvc;
        var core = new GDActionContextCore { Inner = mock };

        core.Quests.RemoveTag("q", "old_tag");

        Assert.Equal("q", questSvc.LastRemovedTagId);
        Assert.Equal("old_tag", questSvc.LastRemovedTag);
    }

    [Fact]
    public void QuestNotify_DelegatesToInner()
    {
        var mock = new MockActionContext();
        var questSvc = new MockQuestService();
        mock.Quests = questSvc;
        var core = new GDActionContextCore { Inner = mock };

        core.Quests.SendNotification("q_notify");

        Assert.Equal("q_notify", questSvc.LastNotificationId);
    }

    [Fact]
    public void CharAddToParty_DelegatesToInner()
    {
        var mock = new MockActionContext();
        var charSvc = new MockCharacterService();
        mock.Character = charSvc;
        var core = new GDActionContextCore { Inner = mock };

        core.Character.AddToParty("marao");

        Assert.Equal("marao", charSvc.LastAddToPartyType);
    }

    [Fact]
    public void CharNotify_DelegatesToInner()
    {
        var mock = new MockActionContext();
        var charSvc = new MockCharacterService();
        mock.Character = charSvc;
        var core = new GDActionContextCore { Inner = mock };

        core.Character.SendNotification("al_azif");

        Assert.Equal("al_azif", charSvc.LastNotifyType);
    }

    [Fact]
    public void BattleInit_DelegatesToInner_WithAllArgs()
    {
        var mock = new MockActionContext();
        var battleSvc = new MockBattleService();
        mock.Battle = battleSvc;
        var core = new GDActionContextCore { Inner = mock };

        core.Battle.Init("battle_scene", "enemy1,enemy2", "quest_bind", "started");

        Assert.Equal("battle_scene", battleSvc.LastSceneType);
        Assert.Equal("enemy1,enemy2", battleSvc.LastEnemies);
        Assert.Equal("quest_bind", battleSvc.LastQuestId);
        Assert.Equal("started", battleSvc.LastNewTag);
    }

    [Fact]
    public void BattleInit_DelegatesToInner_EmptyOptionalArgsBecomeNull()
    {
        var mock = new MockActionContext();
        var battleSvc = new MockBattleService();
        mock.Battle = battleSvc;
        var core = new GDActionContextCore { Inner = mock };

        core.Battle.Init("s", "e", "", "");

        Assert.Equal("s", battleSvc.LastSceneType);
        Assert.Equal("e", battleSvc.LastEnemies);
        Assert.Null(battleSvc.LastQuestId);
        Assert.Null(battleSvc.LastNewTag);
    }

    [Fact]
    public void LightingClearMap_DelegatesToInner()
    {
        var mock = new MockActionContext();
        var lightSvc = new MockLightingService();
        mock.Lighting = lightSvc;
        var core = new GDActionContextCore { Inner = mock };

        core.Lighting.ClearDepthMap();

        Assert.True(lightSvc.ClearMapCalled);
    }

    [Fact]
    public void LightingSetTime_DelegatesToInner()
    {
        var mock = new MockActionContext();
        var lightSvc = new MockLightingService();
        mock.Lighting = lightSvc;
        var core = new GDActionContextCore { Inner = mock };

        core.Lighting.SetUseTime(true);
        Assert.True(lightSvc.UseTimeValue);

        core.Lighting.SetUseTime(false);
        Assert.False(lightSvc.UseTimeValue);
    }

    [Fact]
    public void SceneInstantiate_DelegatesToInner()
    {
        var mock = new MockActionContext();
        var sceneSvc = new MockSceneNodeService();
        mock.Scene = sceneSvc;
        var core = new GDActionContextCore { Inner = mock };

        core.Scene.Instantiate("res://scenes/test.tscn", "test_node");

        Assert.Equal("res://scenes/test.tscn", sceneSvc.LastInstPath);
        Assert.Equal("test_node", sceneSvc.LastInstName);
    }

    [Fact]
    public void SceneRemove_DelegatesToInner()
    {
        var mock = new MockActionContext();
        var sceneSvc = new MockSceneNodeService();
        mock.Scene = sceneSvc;
        var core = new GDActionContextCore { Inner = mock };

        core.Scene.Remove("old_node");

        Assert.Equal("old_node", sceneSvc.LastRemoveName);
    }
}
#endif
