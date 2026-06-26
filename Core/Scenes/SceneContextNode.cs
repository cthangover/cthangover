using System.Collections.Generic;
using Cthangover.Core.Interactive;
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
			if (Instance != null && GodotObject.IsInstanceValid(Instance))
			{
				var scene = GetTree()?.CurrentScene?.Name ?? "?";
				var existingPath = Instance.GetPath().ToString();
				var myPath = GetPath().ToString();
				GameLogger.Log("DUPLICATE", $"SceneContextNode._Ready: Instance ALREADY SET by '{existingPath}', ignoring duplicate at '{myPath}' on scene '{scene}'", LogLevel.Error);
				return;
			}
			Instance = this;
			ProcessMode = ProcessModeEnum.Always;
			_lastSceneRoot = GetTree()?.CurrentScene;

			InteractiveManager.Initialize();
			GameLogger.Log("SCENE_CTX", "_Ready: InteractiveManager initialized");
		}

		public override void _ExitTree()
		{
			if (Instance == this)
				Instance = null;
		}

		public override void _Process(double delta)
		{
			TryClearOnSceneChange();
		}

		private void TryClearOnSceneChange()
		{
			var currentRoot = GetTree()?.CurrentScene;
			if (currentRoot == null)
				return;

			var lastSceneRootDisposed = _lastSceneRoot != null && !GodotObject.IsInstanceValid(_lastSceneRoot);
			var sceneChanged = _lastSceneRoot == null || lastSceneRootDisposed || currentRoot != _lastSceneRoot;

			if (sceneChanged)
			{
				var oldName = _lastSceneRoot != null && !lastSceneRootDisposed
					? _lastSceneRoot.Name.ToString()
					: "NULL/DISPOSED";
				GameLogger.Log("SCENE_CTX", $"TryClearOnSceneChange: scene changed '{oldName}' -> '{currentRoot.Name}', clearing data");
				_lastSceneRoot = currentRoot;
				ClearData();
			}
		}

		public void ClearData()
		{
			GameLogger.Log("SCENE_CTX", $"ClearData: data={data.Count}, notFoundCache={notFoundCache.Count}, eventObjects={eventObjects.Count}");
			InteractiveManager.Shutdown();
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
			if (Instance == null || !GodotObject.IsInstanceValid(Instance))
				return null;
			return Instance.GetSafeNode<T>(nodeName);
		}

		public T GetSceneRoot<T>(string groupName) where T : Node
		{
			TryClearOnSceneChange();

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
			TryClearOnSceneChange();

			if (data.TryGetValue(nodeName, out var value) && value != null)
			{
				if (value is GodotObject go && GodotObject.IsInstanceValid(go))
				{
					GameLogger.Log("SCENE_CTX", $"GetSafeNode<{typeof(T).Name}>('{nodeName}') -> CACHE HIT (valid)");
					return (T)value;
				}
				GameLogger.Log("SCENE_CTX", $"GetSafeNode<{typeof(T).Name}>('{nodeName}') -> CACHE STALE, removing");
				data.Remove(nodeName);
			}

			if (notFoundCache.Contains(nodeName))
			{
				GameLogger.Log("SCENE_CTX", $"GetSafeNode<{typeof(T).Name}>('{nodeName}') -> NOTFOUND CACHE HIT, returning null");
				return null;
			}

			var tree = GetTree();
			var root = tree?.Root;
			if (root == null)
			{
				GameLogger.Log("SCENE_CTX", $"GetSafeNode<{typeof(T).Name}>('{nodeName}') -> root is NULL (tree={(tree != null ? "exists" : "NULL")})", LogLevel.Error);
				return null;
			}

			GameLogger.Log("SCENE_CTX", $"GetSafeNode<{typeof(T).Name}>('{nodeName}') -> searching tree (root children={root.GetChildCount()}, CurrentScene={tree.CurrentScene?.Name ?? "NULL"})", LogLevel.Debug);

			T v = root.FindChild(nodeName, true, false) as T;
			if (v == null)
			{
				notFoundCache.Add(nodeName);
				GameLogger.Log("SCENE", $"node '{nodeName}' of type {typeof(T).Name} not found", LogLevel.Error);
				return null;
			}
			GameLogger.Log("SCENE_CTX", $"GetSafeNode<{typeof(T).Name}>('{nodeName}') -> FOUND and cached (parent={v.GetParent()?.Name ?? "NULL"})", LogLevel.Debug);
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
