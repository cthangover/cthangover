#if TOOLS
using System.Collections.Generic;
using Cthangover.Core.Actions;
using Cthangover.Core.Mods;
using Cthangover.Core.Quests;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Autotest
{
	public partial class GDActionContextCoreTestSetup : Node
	{
		private int _passed;
		private int _failed;

		public override void _Ready()
		{
			GameLogger.Log("TEST", "GDActionContextCoreTestSetup: starting action context delegation tests");

			Test_GetParam_DelegatesToInner();
			Test_Log_DelegatesToInner();
			Test_LogWarning_DelegatesToInner();
			Test_QuestExists_DelegatesToInner();
			Test_QuestSetStatus_DelegatesToInner();
			Test_QuestSetDataStatus_DelegatesToInner();
			Test_QuestAddTag_DelegatesToInner();
			Test_QuestRemoveTag_DelegatesToInner();
			Test_QuestNotify_DelegatesToInner();
			Test_CharAddToParty_DelegatesToInner();
			Test_CharNotify_DelegatesToInner();
			Test_BattleInit_DelegatesToInner_WithAllArgs();
			Test_BattleInit_DelegatesToInner_EmptyOptionalArgsBecomeNull();
			Test_LightingClearMap_DelegatesToInner();
			Test_LightingSetTime_DelegatesToInner();
			Test_SceneInstantiate_DelegatesToInner();
			Test_SceneRemove_DelegatesToInner();

			GameLogger.Log("TEST", $"GDActionContextCoreTestSetup: {_passed} passed, {_failed} failed");

			if (_failed == 0)
				GameLogger.Log("TEST", "All GDActionContextCore tests PASSED");

			GetTree().Quit();
		}

		private void Test_GetParam_DelegatesToInner()
		{
			var mock = new MockActionContext();
			mock.SetParam("key1", "value1");
			mock.SetParam("key2", "");
			var core = new GDActionContextCore { Inner = mock };

			AssertEqual("value1", core.GetParam("key1"), "GetParam key1");
			AssertEqual("", core.GetParam("key2"), "GetParam key2 empty");
			AssertNull(core.GetParam("missing"), "GetParam missing");
		}

		private void Test_Log_DelegatesToInner()
		{
			var mock = new MockActionContext();
			var core = new GDActionContextCore { Inner = mock };

			core.Log("CAT", "msg");
			core.Log("EVENT", "another");

			AssertEqual(2, mock.Logs.Count, "Log count");
			AssertEqual("CAT", mock.Logs[0].Category, "Log[0] category");
			AssertEqual("msg", mock.Logs[0].Message, "Log[0] message");
			AssertEqual("EVENT", mock.Logs[1].Category, "Log[1] category");
			AssertEqual("another", mock.Logs[1].Message, "Log[1] message");
		}

		private void Test_LogWarning_DelegatesToInner()
		{
			var mock = new MockActionContext();
			var core = new GDActionContextCore { Inner = mock };

			core.LogWarning("WARN", "oops");

			AssertEqual(1, mock.Warnings.Count, "LogWarning count");
			AssertEqual("WARN", mock.Warnings[0].Category, "LogWarning[0] category");
			AssertEqual("oops", mock.Warnings[0].Message, "LogWarning[0] message");
		}

		private void Test_QuestExists_DelegatesToInner()
		{
			var mock = new MockActionContext();
			var questSvc = new MockQuestService();
			questSvc.ExistsResult = true;
			mock.Quests = questSvc;
			var core = new GDActionContextCore { Inner = mock };

			AssertTrue(core.Quests.Exists("q1"), "Quests.Exists q1=true");

			questSvc.ExistsResult = false;
			AssertFalse(core.Quests.Exists("q2"), "Quests.Exists q2=false");
		}

		private void Test_QuestSetStatus_DelegatesToInner()
		{
			var mock = new MockActionContext();
			var questSvc = new MockQuestService();
			mock.Quests = questSvc;
			var core = new GDActionContextCore { Inner = mock };

			core.Quests.SetStatus("quest_1", "Active");

			AssertEqual("quest_1", questSvc.LastSetStatusId, "SetStatus id");
			AssertEqual("Active", questSvc.LastSetStatusStatus, "SetStatus status");
		}

		private void Test_QuestSetDataStatus_DelegatesToInner()
		{
			var mock = new MockActionContext();
			var questSvc = new MockQuestService();
			mock.Quests = questSvc;
			var core = new GDActionContextCore { Inner = mock };

			core.Quests.SetDataStatus("q", 5);

			AssertEqual("q", questSvc.LastDataStatusId, "SetDataStatus id");
			AssertEqual(5, questSvc.LastDataStatusLevel, "SetDataStatus level");
		}

		private void Test_QuestAddTag_DelegatesToInner()
		{
			var mock = new MockActionContext();
			var questSvc = new MockQuestService();
			mock.Quests = questSvc;
			var core = new GDActionContextCore { Inner = mock };

			core.Quests.AddTag("q", "found_key");

			AssertEqual("q", questSvc.LastAddedTagId, "AddTag id");
			AssertEqual("found_key", questSvc.LastAddedTag, "AddTag tag");
		}

		private void Test_QuestRemoveTag_DelegatesToInner()
		{
			var mock = new MockActionContext();
			var questSvc = new MockQuestService();
			mock.Quests = questSvc;
			var core = new GDActionContextCore { Inner = mock };

			core.Quests.RemoveTag("q", "old_tag");

			AssertEqual("q", questSvc.LastRemovedTagId, "RemoveTag id");
			AssertEqual("old_tag", questSvc.LastRemovedTag, "RemoveTag tag");
		}

		private void Test_QuestNotify_DelegatesToInner()
		{
			var mock = new MockActionContext();
			var questSvc = new MockQuestService();
			mock.Quests = questSvc;
			var core = new GDActionContextCore { Inner = mock };

			core.Quests.SendNotification("q_notify");

			AssertEqual("q_notify", questSvc.LastNotificationId, "SendNotification id");
		}

		private void Test_CharAddToParty_DelegatesToInner()
		{
			var mock = new MockActionContext();
			var charSvc = new MockCharacterService();
			mock.Character = charSvc;
			var core = new GDActionContextCore { Inner = mock };

			core.Character.AddToParty("marao");

			AssertEqual("marao", charSvc.LastAddToPartyType, "AddToParty type");
		}

		private void Test_CharNotify_DelegatesToInner()
		{
			var mock = new MockActionContext();
			var charSvc = new MockCharacterService();
			mock.Character = charSvc;
			var core = new GDActionContextCore { Inner = mock };

			core.Character.SendNotification("al_azif");

			AssertEqual("al_azif", charSvc.LastNotifyType, "SendNotification type");
		}

		private void Test_BattleInit_DelegatesToInner_WithAllArgs()
		{
			var mock = new MockActionContext();
			var battleSvc = new MockBattleService();
			mock.Battle = battleSvc;
			var core = new GDActionContextCore { Inner = mock };

			core.Battle.Init("battle_scene", "enemy1,enemy2", "quest_bind", "started");

			AssertEqual("battle_scene", battleSvc.LastSceneType, "Init sceneType");
			AssertEqual("enemy1,enemy2", battleSvc.LastEnemies, "Init enemies");
			AssertEqual("quest_bind", battleSvc.LastQuestId, "Init questId");
			AssertEqual("started", battleSvc.LastNewTag, "Init newTag");
		}

		private void Test_BattleInit_DelegatesToInner_EmptyOptionalArgsBecomeNull()
		{
			var mock = new MockActionContext();
			var battleSvc = new MockBattleService();
			mock.Battle = battleSvc;
			var core = new GDActionContextCore { Inner = mock };

			core.Battle.Init("s", "e", "", "");

			AssertEqual("s", battleSvc.LastSceneType, "Init sceneType (empty optionals)");
			AssertEqual("e", battleSvc.LastEnemies, "Init enemies (empty optionals)");
			AssertNull(battleSvc.LastQuestId, "Init questId null");
			AssertNull(battleSvc.LastNewTag, "Init newTag null");
		}

		private void Test_LightingClearMap_DelegatesToInner()
		{
			var mock = new MockActionContext();
			var lightSvc = new MockLightingService();
			mock.Lighting = lightSvc;
			var core = new GDActionContextCore { Inner = mock };

			core.Lighting.ClearDepthMap();

			AssertTrue(lightSvc.ClearMapCalled, "ClearDepthMap called");
		}

		private void Test_LightingSetTime_DelegatesToInner()
		{
			var mock = new MockActionContext();
			var lightSvc = new MockLightingService();
			mock.Lighting = lightSvc;
			var core = new GDActionContextCore { Inner = mock };

			core.Lighting.SetUseTime(true);
			AssertTrue(lightSvc.UseTimeValue == true, "SetUseTime true");

			core.Lighting.SetUseTime(false);
			AssertTrue(lightSvc.UseTimeValue == false, "SetUseTime false");
		}

		private void Test_SceneInstantiate_DelegatesToInner()
		{
			var mock = new MockActionContext();
			var sceneSvc = new MockSceneNodeService();
			mock.Scene = sceneSvc;
			var core = new GDActionContextCore { Inner = mock };

			core.Scene.Instantiate("res://scenes/test.tscn", "test_node");

			AssertEqual("res://scenes/test.tscn", sceneSvc.LastInstPath, "Instantiate path");
			AssertEqual("test_node", sceneSvc.LastInstName, "Instantiate name");
		}

		private void Test_SceneRemove_DelegatesToInner()
		{
			var mock = new MockActionContext();
			var sceneSvc = new MockSceneNodeService();
			mock.Scene = sceneSvc;
			var core = new GDActionContextCore { Inner = mock };

			core.Scene.Remove("old_node");

			AssertEqual("old_node", sceneSvc.LastRemoveName, "Remove name");
		}

		private void AssertEqual(string expected, string actual, string label)
		{
			if (expected == actual)
				Pass($"{label}: '{expected}'");
			else
				Fail($"{label}: expected '{expected}', got '{actual}'");
		}

		private void AssertEqual(int expected, int actual, string label)
		{
			if (expected == actual)
				Pass($"{label}: {expected}");
			else
				Fail($"{label}: expected {expected}, got {actual}");
		}

		private void AssertNull(object value, string label)
		{
			if (value == null)
				Pass($"{label}: null");
			else
				Fail($"{label}: expected null, got '{value}'");
		}

		private void AssertTrue(bool condition, string label)
		{
			if (condition)
				Pass($"{label}: true");
			else
				Fail($"{label}: expected true, got false");
		}

		private void AssertFalse(bool condition, string label)
		{
			if (!condition)
				Pass($"{label}: false");
			else
				Fail($"{label}: expected false, got true");
		}

		private void Pass(string msg)
		{
			_passed++;
			GameLogger.Log("TEST", $"  [PASS] {msg}");
		}

		private void Fail(string msg)
		{
			_failed++;
			GameLogger.Log("TEST", $"  [FAIL] {msg}", LogLevel.Error);
		}
	}

	internal class MockActionContext : IActionContext
	{
		private readonly Dictionary<string, string> _params = new();

		public List<(string Category, string Message)> Logs { get; } = new();
		public List<(string Category, string Message)> Warnings { get; } = new();

		public IQuestService Quests { get; set; }
		public ICharacterService Character { get; set; }
		public IBattleService Battle { get; set; }
		public ILightingService Lighting { get; set; }
		public ISceneNodeService Scene { get; set; }
		public IModRegistry ModRegistry { get; set; }
		public Items.IInventory Inventory { get; set; }

		public string GetParam(string name)
		{
			_params.TryGetValue(name, out var value);
			return value;
		}

		public void SetParam(string name, string value) => _params[name] = value;

		public void Log(string category, string message) => Logs.Add((category, message));
		public void LogWarning(string category, string message) => Warnings.Add((category, message));
	}

	internal class MockQuestService : IQuestService
	{
		public string LastSetStatusId { get; private set; }
		public string LastSetStatusStatus { get; private set; }
		public string LastAddedTagId { get; private set; }
		public string LastAddedTag { get; private set; }
		public string LastRemovedTagId { get; private set; }
		public string LastRemovedTag { get; private set; }
		public string LastNotificationId { get; private set; }
		public int LastDataStatusLevel { get; private set; }
		public string LastDataStatusId { get; private set; }
		public bool ExistsResult { get; set; } = true;

		public IQuest Get(string id) => null;

		public bool Exists(string id) => ExistsResult;

		public void SetStatus(string id, string status)
		{
			LastSetStatusId = id;
			LastSetStatusStatus = status;
		}

		public void SetDataStatus(string id, int level)
		{
			LastDataStatusId = id;
			LastDataStatusLevel = level;
		}

		public void AddTag(string id, string tag)
		{
			LastAddedTagId = id;
			LastAddedTag = tag;
		}

		public void RemoveTag(string id, string tag)
		{
			LastRemovedTagId = id;
			LastRemovedTag = tag;
		}

		public void SendNotification(string id) => LastNotificationId = id;
	}

	internal class MockCharacterService : ICharacterService
	{
		public string LastAddToPartyType { get; private set; }
		public string LastNotifyType { get; private set; }

		public void AddToParty(string type) => LastAddToPartyType = type;
		public void SendNotification(string type) => LastNotifyType = type;
	}

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

	internal class MockLightingService : ILightingService
	{
		public bool ClearMapCalled { get; private set; }
		public bool? UseTimeValue { get; private set; }

		public void ClearDepthMap() => ClearMapCalled = true;
		public void SetUseTime(bool useTime) => UseTimeValue = useTime;
	}

	internal class MockSceneNodeService : ISceneNodeService
	{
		public string LastInstPath { get; private set; }
		public string LastInstName { get; private set; }
		public string LastRemoveName { get; private set; }

		public void Instantiate(string scenePath, string nodeName)
		{
			LastInstPath = scenePath;
			LastInstName = nodeName;
		}

		public void Remove(string nodeName) => LastRemoveName = nodeName;

		public T Find<T>(string nodeName) where T : Node => null;
	}
}
#endif
