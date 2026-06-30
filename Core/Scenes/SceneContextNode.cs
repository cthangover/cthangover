using System.Collections.Generic;
using Cthangover.Core.Interactive;
using Cthangover.Core.UI.Executable;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Scenes
{

	/// <summary>
	/// Persistent autoload-style singleton node that caches scene-tree node lookups
	/// and manages interactive event objects. Automatically detects Godot scene
	/// changes (via <see cref="_Process"/>) and clears all cached references,
	/// ensuring stale nodes are never returned. Provides static helpers like
	/// <see cref="FindNode{T}"/> for convenience access throughout the codebase.
	/// The singleton instance is set during <see cref="_Ready"/> and cleared on
	/// <see cref="_ExitTree"/>.
	/// </summary>
	public partial class SceneContextNode : Node
	{

		/// <summary>
		/// The singleton instance of <see cref="SceneContextNode"/>. Only one instance
		/// is allowed; duplicates log an error and self-disable.
		/// </summary>
		public static SceneContextNode Instance { get; set; }

		/// <summary>
		/// Tracks the most recently applied background texture ID, used by save/restore
		/// flows and lazy reload logic in <see cref="SceneManager"/>.
		/// </summary>
		public static string LastBackgroundID { get; set; }

		/// <summary>
		/// Returns the root node of the currently active Godot scene, or <c>null</c>
		/// if no scene tree is available.
		/// </summary>
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

		/// <summary>
		/// Destroys all tracked interactive event objects via <see cref="IEventObject.Destruct"/>,
		/// clears the node-lookup cache and the not-found cache, and shuts down the
		/// <see cref="InteractiveManager"/>. Called automatically when the active
		/// Godot scene changes.
		/// </summary>
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

		/// <summary>
		/// Statically searches the entire scene tree for a node by name and type,
		/// using the singleton instance's <see cref="GetSafeNode{T}"/>. Returns
		/// <c>null</c> if the instance is unavailable or the node is not found.
		/// </summary>
		/// <typeparam name="T">The expected Godot <see cref="Node"/> type.</typeparam>
		/// <param name="nodeName">The name of the node to find.</param>
		public static T FindNode<T>(string nodeName) where T : Node
		{
			if (Instance == null || !GodotObject.IsInstanceValid(Instance))
				return null;
			return Instance.GetSafeNode<T>(nodeName);
		}

		/// <summary>
		/// Resolves the first node in a Godot group, caching the result in the internal
		/// data dictionary. Re-checks on scene change and validates the cached Godot
		/// object reference to avoid returning disposed nodes.
		/// </summary>
		/// <typeparam name="T">The expected Godot <see cref="Node"/> type.</typeparam>
		/// <param name="groupName">The Godot group name to query.</param>
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

		/// <summary>
		/// Performs a cached recursive node lookup through the scene tree root. On first
		/// call for a given name, searches the tree via <see cref="Node.FindChild"/> and
		/// caches the result. Subsequent calls return the cached reference after validating
		/// it is still a valid Godot object. Failed lookups are stored in a not-found cache
		/// to avoid repeated searches within the same scene lifecycle.
		/// </summary>
		/// <typeparam name="T">The expected Godot <see cref="Node"/> type.</typeparam>
		/// <param name="nodeName">The name of the node to find.</param>
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

		/// <summary>
		/// Destroys and removes a tracked interactive event object by its identifier.
		/// Calls <see cref="IEventObject.Destruct"/> before removal.
		/// </summary>
		/// <param name="id">The unique identifier of the event object to remove.</param>
		public void RemoveEventObject(string id)
		{
			if(eventObjects.TryGetValue(id, out var obj))
				obj.Destruct();
			eventObjects.Remove(id);
		}

		/// <summary>
		/// Removes any entries from the event object dictionary whose values have
		/// become <c>null</c>, preventing accumulation of garbage entries.
		/// </summary>
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
