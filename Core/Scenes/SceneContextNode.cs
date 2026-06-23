using System.Collections.Generic;
using Cthangover.Core.Audio;
using Cthangover.Core.Battle;
using Cthangover.Core.UI.Animation;
using Cthangover.Core.UI.Dialog;
using Cthangover.Core.UI.Event;
using Cthangover.Core.UI.Executable;
using Cthangover.Core.UI.Lights;
using Cthangover.Core.UI.Menu;
using Cthangover.Core.UI.Tool;
using Cthangover.Core.UI.View;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Scenes
{

	public partial class SceneContextNode : Node
	{

		public static SceneContextNode Instance { get; set; }

		public static string LastBackgroundID { get; set; }

		public static Godot.Node CurrentScene => Instance?.GetTree()?.CurrentScene;

		private Dictionary<string, object> data = new();
		private Dictionary<string, IEventObject> eventObjects = new();
		private HashSet<string> notFoundCache = new();

		public override void _EnterTree()
		{
			base._EnterTree();
			Instance = this;
			ClearData();
		}

		public override void _ExitTree()
		{
			base._ExitTree();
			ClearData();
		}

		public void ClearData()
		{
			data.Clear();
			if (Lists.IsNotEmpty(eventObjects))
			{
				foreach (var entry in eventObjects)
					entry.Value.Destruct();
				eventObjects.Clear();
			}
			notFoundCache.Clear();
		}

		public static T FindNode<T>(string nodeName) where T : Node
		{
			if (Instance == null)
				return null;
			return Instance.GetSafeNode<T>(nodeName);
		}

		public T GetSceneRoot<T>(string groupName) where T : Node
		{
			if (!data.TryGetValue(groupName, out var value) || value == null)
			{
				var nodes = GetTree().GetNodesInGroup(groupName);
				if (nodes.Count > 0)
				{
					var v = nodes[0] as T;
					data[groupName] = v;
					return v;
				}
				GameLogger.Log("SCENE", $"object with group '{groupName}' not found", LogLevel.Error);
				return null;
			}
			return (T)value;
		}

		public T GetSafeNode<T>(string nodeName) where T : Node
		{
			if (!data.TryGetValue(nodeName, out var value) || value == null)
			{
				if (notFoundCache.Contains(nodeName))
					return null;

				var root = GetTree()?.Root;
				if (root == null)
					return null;

				T v = root.FindChild(nodeName, true, false) as T;
				if (v == null)
				{
					notFoundCache.Add(nodeName);
					GameLogger.Log("SCENE", $"node '{nodeName}' of type {typeof(T).Name} not found", LogLevel.Error);
					return null;
				}
				data[nodeName] = v;
				return v;
			}
			return (T)value;
		}

		public IEventObject GetEventObject(string id)
		{
			return eventObjects[id];
		}

		public void AddEventObject(IEventObject obj)
		{
			if(eventObjects.ContainsKey(obj.ID))
			{
				GameLogger.Log("SCENE", "event object '" + obj.ID + "' already exists!", LogLevel.Warning);
			}
			eventObjects[obj.ID] = obj;
		}

		public void RemoveEventObject(IEventObject obj)
		{
			RemoveEventObject(obj.ID);
		}

		public void RemoveEventObject(string id)
		{
			if(eventObjects.TryGetValue(id, out var obj))
				obj.Destruct();
			eventObjects.Remove(id);
		}

		public void OptimizeEventObjects()
		{
			var keysToRemove = new List<string>();
			foreach (var pair in eventObjects)
				if (pair.Value == null)
					keysToRemove.Add(pair.Key);
			foreach (var key in keysToRemove)
				eventObjects.Remove(key);
		}
	}

}
