using System.Collections.Generic;
using Cthangover.Core.Mods;
using Cthangover.Core.Scenarios;
using Cthangover.Core.Scenes;
using Cthangover.Core.UI.Dialog;
using Cthangover.Core.UI.View;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Interactive
{
	/// <summary>
	/// Central registry for active interactive objects on the current scene.
	/// Manages the full lifecycle: creation from <c>InteractiveDefinition</c>,
	/// placement into <c>ViewBox</c> layer containers, callback wiring for
	/// hover/click actions, and cleanup on scene change.
	///
	/// Created and owned by <c>SceneContextNode</c> so its state is
	/// automatically cleared when the scene changes.
	///
	/// Access via <c>InteractiveManager.Instance</c> (set during
	/// <c>SceneContextNode._Ready</c>).
	/// </summary>
	public class InteractiveManager
	{
		private static InteractiveManager _instance;

		/// <summary>
		/// Singleton instance, valid only while a scene is active.
		/// Set by <c>SceneContextNode</c> on scene enter, cleared on scene exit.
		/// </summary>
		public static InteractiveManager Instance => _instance;

		private readonly Dictionary<string, InteractiveObject> _objects = new();
		private bool _showDebugBounds;

		/// <summary>
		/// Initialises the singleton and performs one-time setup.
		/// Called by <c>SceneContextNode._Ready</c>.
		/// </summary>
		public static void Initialize()
		{
			_instance = new InteractiveManager();
		}

		/// <summary>
		/// Tears down active objects but keeps the singleton alive.
		/// Called by <c>SceneContextNode.ClearData</c> on scene change.
		/// The manager instance persists for the full game session; only
		/// the tracked objects are cleaned up on each scene transition.
		/// </summary>
		public static void Shutdown()
		{
			_instance?.ClearAll();
		}

		/// <summary>
		/// Toggles debug visualisation of hit-area bounds and object IDs.
		/// When enabled, every active object draws a coloured overlay.
		/// </summary>
		public bool ShowDebugBounds
		{
			get => _showDebugBounds;
			set
			{
				_showDebugBounds = value;
				foreach (var kv in _objects)
					kv.Value.UpdateDebugVisual(value);
			}
		}

		/// <summary>
		/// Number of currently active interactive objects.
		/// </summary>
		public int Count => _objects.Count;

		/// <summary>
		/// Instantiates an interactive object from its definition ID and adds it
		/// to the appropriate <c>InteractiveLayer</c> in the <c>ViewBox</c>.
		/// If an object with the same ID is already active, returns the existing one.
		/// </summary>
		/// <param name="definitionId">The <c>ID</c> from the <c>InteractiveDefinition</c> JSON.</param>
		/// <returns>The created or existing <c>InteractiveObject</c>, or <c>null</c> if the definition is not found.</returns>
		public InteractiveObject Add(string definitionId)
		{
			if (string.IsNullOrEmpty(definitionId))
				return null;

			if (_objects.TryGetValue(definitionId, out var existing))
				return existing;

			var def = InteractiveFactory.Instance.Get(definitionId);
			if (def == null)
			{
				GameLogger.Log("INTERACTIVE", $"Add: definition '{definitionId}' not found", LogLevel.Error);
				return null;
			}

			var viewBox = SceneContextNode.FindNode<ViewBox>("ViewBox");
			if (viewBox == null)
			{
				GameLogger.Log("INTERACTIVE", "Add: ViewBox not found", LogLevel.Error);
				return null;
			}

			var layerContainer = viewBox.GetInteractiveLayer(def.Layer);
			if (layerContainer == null)
			{
				GameLogger.Log("INTERACTIVE", $"Add: layer '{def.Layer}' not found, trying foreground", LogLevel.Warning);
				layerContainer = viewBox.GetInteractiveLayer("foreground");
			}

			if (layerContainer == null)
			{
				GameLogger.Log("INTERACTIVE", "Add: no interactive layer containers available (ViewBox layers not ready?)", LogLevel.Error);
				return null;
			}

			var contentSize = viewBox.LogicalSize != Vector2I.Zero
				? new Vector2(viewBox.LogicalSize.X, viewBox.LogicalSize.Y)
				: viewBox.Content?.Size ?? new Vector2(1920f, 1024f);

			var obj = new InteractiveObject
			{
				Name = "Interactive_" + def.ID,
				ID = def.ID
			};

			obj.OnHoverEnterCallback = ExecuteHoverDsl;
			obj.OnHoverLeaveCallback = ExecuteHoverDsl;
			obj.OnClickCallback = ExecuteClick;

			layerContainer.AddChild(obj);
			obj.Configure(def, contentSize);

			_objects[definitionId] = obj;

			GameLogger.Log("INTERACTIVE", $"Add: '{definitionId}' created on layer '{def.Layer}' fullscreen={contentSize.X:F0}x{contentSize.Y:F0} hitType={def.HitArea?.Type ?? "rect"}");
			return obj;
		}

		/// <summary>
		/// Removes and destroys the interactive object with the given definition ID.
		/// </summary>
		/// <param name="definitionId">The definition ID to remove.</param>
		public void Remove(string definitionId)
		{
			if (_objects.TryGetValue(definitionId, out var obj))
			{
				GameLogger.Log("INTERACTIVE", $"Remove: '{definitionId}'");
				obj.Destruct();
				_objects.Remove(definitionId);
			}
		}

		/// <summary>
		/// Removes and destroys all active interactive objects.
		/// Called automatically on scene change via <c>Shutdown</c>.
		/// </summary>
		public void ClearAll()
		{
			GameLogger.Log("INTERACTIVE", $"ClearAll: destroying {_objects.Count} object(s)");
			foreach (var kv in _objects)
				kv.Value.Destruct();
			_objects.Clear();
		}

		/// <summary>
		/// Looks up an active interactive object by its definition ID.
		/// </summary>
		/// <param name="definitionId">The definition ID to find.</param>
		/// <returns>The <c>InteractiveObject</c> or <c>null</c>.</returns>
		public InteractiveObject Get(string definitionId)
		{
			_objects.TryGetValue(definitionId, out var obj);
			return obj;
		}

		/// <summary>
		/// Enables or disables an active interactive object.
		/// </summary>
		/// <param name="definitionId">The definition ID.</param>
		/// <param name="enabled">New enabled state.</param>
		public void SetEnabled(string definitionId, bool enabled)
		{
			if (_objects.TryGetValue(definitionId, out var obj))
				obj.IsEnabled = enabled;
		}

		/// <summary>
		/// Shows or hides an active interactive object.
		/// </summary>
		/// <param name="definitionId">The definition ID.</param>
		/// <param name="visible">New visibility state.</param>
		public void SetVisible(string definitionId, bool visible)
		{
			if (_objects.TryGetValue(definitionId, out var obj))
				obj.Visible = visible;
		}

		private void ExecuteHoverDsl(string dsl)
		{
			if (string.IsNullOrEmpty(dsl))
				return;

			GameLogger.Log("INTERACTIVE", $"ExecuteHoverDsl: '{dsl}'");
			ExecuteInlineCommands(dsl);
		}

		private void ExecuteClick(string definitionId)
		{
			if (string.IsNullOrEmpty(definitionId))
				return;

			var def = InteractiveFactory.Instance.Get(definitionId);
			if (def?.Actions?.OnClick == null)
			{
				GameLogger.Log("INTERACTIVE", $"ExecuteClick: no action defined for '{definitionId}'");
				return;
			}

			var clickAction = def.Actions.OnClick;

			var hasScenario = !string.IsNullOrEmpty(clickAction.Scenario);
			var hasCommands = clickAction.Commands != null && clickAction.Commands.Length > 0;

			if (!hasScenario && !hasCommands)
				return;

			if (!hasScenario && hasCommands)
			{
				var inlineDsl = string.Join("\n", clickAction.Commands);
				ExecuteInlineCommands(inlineDsl);
				return;
			}

			var dialogBox = SceneContextNode.FindNode<DialogBox>("DialogBox");
			if (dialogBox == null)
			{
				GameLogger.Log("INTERACTIVE", "ExecuteClick: DialogBox not found", LogLevel.Error);
				return;
			}

			if (dialogBox.IsRunning)
			{
				GameLogger.Log("INTERACTIVE", "ExecuteClick: dialog already running, skipping");
				return;
			}

			var dlg = BuildClickDialog(def);

			if (dlg != null && dlg.Queue.Count > 0)
				dialogBox.SetDialogQueueAndRun(dlg, null, 0, null);
		}

		private DialogQueue BuildClickDialog(InteractiveDefinition def)
		{
			if (def?.Actions?.OnClick == null)
				return null;

			var clickAction = def.Actions.OnClick;
			var combinedText = "";

			if (!string.IsNullOrEmpty(clickAction.Scenario))
			{
				var modId = def.ModId;
				if (string.IsNullOrEmpty(modId))
					modId = ResolveModIdForScenario(clickAction.Scenario);

				if (!string.IsNullOrEmpty(modId))
				{
					var scenarioText = ModManager.Instance.ReadFileText(modId, clickAction.Scenario);
					if (!string.IsNullOrEmpty(scenarioText))
					{
						var (metadata, body) = ScenarioParser.ParseMetadata(scenarioText);
						combinedText += body + "\n";
					}
				}
			}

			if (clickAction.Commands != null && clickAction.Commands.Length > 0)
			{
				combinedText += string.Join("\n", clickAction.Commands);
			}

			if (string.IsNullOrWhiteSpace(combinedText))
				return null;

			return ScenarioParser.Parse(combinedText);
		}

		private string ResolveModIdForScenario(string scenarioPath)
		{
			var defs = ModManager.Instance.CollectScenarioDefinitions();
			var fileName = System.IO.Path.GetFileNameWithoutExtension(
				scenarioPath.Replace('\\', '/').Split('/')[^1]);

			foreach (var kv in defs)
			{
				foreach (var sd in kv.Value)
				{
					if (sd.Name == fileName)
						return sd.ModId;
				}
			}

			return null;
		}

		private void ExecuteInlineCommands(string dsl)
		{
			if (string.IsNullOrWhiteSpace(dsl))
				return;

			try
			{
				var dlg = ScenarioParser.Parse(dsl);
				if (dlg?.Queue == null || dlg.Queue.Count == 0)
					return;

				var runtime = new DialogRuntime();
				runtime.SetDialogQueueAndRun(null, dlg.Queue, null, 0);
				runtime.Run();
			}
			catch (System.Exception ex)
			{
				GameLogger.Log("INTERACTIVE", $"ExecuteInlineCommands failed: {ex.Message}", LogLevel.Error);
			}
		}

		private readonly Dictionary<string, string> _hoverVariables = new();

		/// <summary>
		/// Simple variable store for hover-event DSL commands (e.g. <c>set cursor_hint=door</c>).
		/// UI elements can subscribe or poll these values to show contextual hints.
		/// </summary>
		public string GetHoverVariable(string name)
		{
			_hoverVariables.TryGetValue(name, out var val);
			return val;
		}

		/// <summary>
		/// Creates a custom interactive object programmatically using a fluent
		/// builder. For use in wrapper-template C# code.
		/// </summary>
		/// <param name="id">Unique instance ID.</param>
		/// <returns>An <c>InteractiveObjectBuilder</c> for fluent configuration.</returns>
		public InteractiveObjectBuilder CreateCustom(string id)
		{
			return new InteractiveObjectBuilder(id, this);
		}

		internal void RegisterBuiltObject(InteractiveObject obj)
		{
			if (obj == null)
				return;

			_objects[obj.ID] = obj;
			obj.OnHoverEnterCallback ??= ExecuteHoverDsl;
			obj.OnHoverLeaveCallback ??= ExecuteHoverDsl;
			obj.OnClickCallback ??= ExecuteClick;
		}
	}
}
