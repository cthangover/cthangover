using System.Collections.Generic;
using Cthangover.Core.UI.Executable;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Scenes
{

	public partial class SceneContextNode : Node
	{

		public static SceneContextNode Instance { get; set; }

		public static string LastBackgroundID { get; set; }

		public static Node CurrentScene => Instance?.GetTree()?.CurrentScene;

		private Dictionary<string, object> data = new();
		private Dictionary<string, IEventObject> eventObjects = new();
		private HashSet<string> notFoundCache = new();
		private Node _lastSceneRoot;

		public override void _Ready()
		{
			Instance = this;
			ProcessMode = ProcessModeEnum.Always;
			_lastSceneRoot = GetTree()?.CurrentScene;
		}

		public override void _Process(double delta)
		{
			var currentRoot = GetTree()?.CurrentScene;
			if (currentRoot != null && currentRoot != _lastSceneRoot)
			{
				_lastSceneRoot = currentRoot;
				ClearData();
			}
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
			if (data.TryGetValue(groupName, out var value) && value != null)
			{
				if (value is GodotObject go && GodotObject.IsInstanceValid(go))
					return (T)value;
				data.Remove(groupName);
			}

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

		public T GetSafeNode<T>(string nodeName) where T : Node
		{
			if (data.TryGetValue(nodeName, out var value) && value != null)
			{
				if (value is GodotObject go && GodotObject.IsInstanceValid(go))
					return (T)value;
				data.Remove(nodeName);
			}

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
