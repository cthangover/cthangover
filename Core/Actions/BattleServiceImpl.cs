using System;
using System.Collections.Generic;
using Cthangover.Core.Battle;
using Cthangover.Core.Factories.Impls;
using Cthangover.Core.Quests;
using Cthangover.Core.Scenes;
using Cthangover.Core.Settings;
using Cthangover.Core.UI.Lights;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Actions
{
		internal class BattleServiceImpl : IBattleService
	{
		public void Init(string sceneType, string enemies, string questId = null, string newTag = null)
		{
			var enemyList = enemies.Split(',');
			var background = BackgroundFactory.Instance.Get(SceneContextNode.LastBackgroundID);
			GameLogger.Log("BATTLE", $"BattleServiceImpl.Init: lastBgId='{SceneContextNode.LastBackgroundID}', bgTexture={(background != null ? "loaded" : "NULL")}", background == null ? LogLevel.Error : LogLevel.Debug);
			var data = BattleData.InitBattle(background, sceneType, enemyList);

			data.DepthMap = UiLightController.Instance?.CurrentDepthMap;
			data.AlbedoMap = UiLightController.Instance?.CurrentAlbedoMap;
			
			GameLogger.Log("BATTLE", $"BattleServiceImpl.Init: depthMap from LightsCtrl = {(data.DepthMap != null ? "captured" : "NULL")}, albedoMap = {(data.AlbedoMap != null ? "captured" : "NULL")}", data.DepthMap == null && data.DepthMap == null ? LogLevel.Error : LogLevel.Debug);

			try
			{
				data.ActiveBattleCore = BattleCoreRegistry.Instance.GetActive()?.Id;
			}
			catch(Exception ex)
			{
				GameLogger.Log("BATTLE", $"BattleServiceImpl.Init: {ex.Message}\n{ex.StackTrace}", LogLevel.Error);
			}

			if (!string.IsNullOrEmpty(questId))
			{
				try
				{
					var quest = QuestFactory.Instance.Get(questId);
					data.Quest = quest;
					if (!string.IsNullOrEmpty(newTag))
					{
						data.NewTag = newTag;
						quest.SendNotification();
					}
				}
				catch (KeyNotFoundException)
				{
					GameLogger.Log("BATTLE", $"BattleInit: quest '{questId}' not found, battle will proceed without quest", LogLevel.Error);
				}
			}

			GameData.Instance.Runtime.BattleData = data;
		}
	}
}
