using Cthangover.Core.Interactive;
using Cthangover.Core.Utils;

namespace Cthangover.Core.UI.Dialog.Action.Impls
{
	/// <summary>
	/// Dialog action that modifies a property of an active interactive object.
	/// Supported properties: <c>enabled</c> (bool), <c>visible</c> (bool).
	/// </summary>
	public class ActionInteractiveSet : ActionCommand
	{
		/// <summary>The definition ID of the interactive object to modify.</summary>
		public string DefinitionId { get; set; }

		/// <summary>Property name: "enabled" or "visible".</summary>
		public string Property { get; set; }

		/// <summary>String value to parse: "true" or "false".</summary>
		public string Value { get; set; }

		public override WaitType WaitType { get; set; } = WaitType.NoWait;

		public override void DoRun(DialogRuntime runtime)
		{
			if (string.IsNullOrEmpty(DefinitionId) || string.IsNullOrEmpty(Property) || string.IsNullOrEmpty(Value))
			{
				GameLogger.Log("INTERACTIVE", "ActionInteractiveSet: missing parameters", LogLevel.Error);
				return;
			}

			var mgr = InteractiveManager.Instance;
			if (mgr == null)
				return;

			if (!bool.TryParse(Value, out var boolVal))
			{
				GameLogger.Log("INTERACTIVE", $"ActionInteractiveSet: invalid bool value '{Value}'", LogLevel.Error);
				return;
			}

			switch (Property.ToLowerInvariant())
			{
				case "enabled":
					mgr.SetEnabled(DefinitionId, boolVal);
					break;
				case "visible":
					mgr.SetVisible(DefinitionId, boolVal);
					break;
				default:
					GameLogger.Log("INTERACTIVE", $"ActionInteractiveSet: unknown property '{Property}'", LogLevel.Error);
					break;
			}
		}
	}
}
