using Cthangover.Core.Characters;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Battle.Actions
{
	/// <summary>
	/// Godot bridge for <see cref="ActionCharacter"/> — battle action
	/// cards. Flattens identity and targeting fields, and exposes the
	/// action's <see cref="PropertyData"/> through typed
	/// GetXxx/SetXxx methods plus a raw <see cref="Properties"/>
	/// dictionary. This mirrors <see cref="GDCharacter"/> so that
	/// GDScript battle executors use the same API to read/write both
	/// character stats and action parameters.
	/// </summary>
	[GlobalClass]
	public partial class GDActionCharacter : RefCounted
	{
		public string Id;
		public string Name;
		public string Description;
		public int Type;

		/// <summary>
		/// Raw string-to-string snapshot of the action's dynamic
		/// properties. Use for iteration and debugging; prefer the
		/// typed <c>GetXxx</c>/<c>SetXxx</c> methods for business logic.
		/// </summary>
		public Godot.Collections.Dictionary<string, string> Properties;

		private PropertyData _props;

		public static GDActionCharacter From(ActionCharacter action)
		{
			var gd = new GDActionCharacter();
			if (action == null)
				return gd;

			gd.Id = action.ID;
			gd.Name = action.Name;
			gd.Description = action.Description;
			gd.Type = (int)action.Type;

			gd._props = action.Properties;
			if (gd._props?.Values != null)
			{
				gd.Properties = new Godot.Collections.Dictionary<string, string>();
				foreach (var kvp in gd._props.Values)
					gd.Properties[kvp.Key] = kvp.Value;
			}

			return gd;
		}

		public string GetStr(string key, string defaultValue = null)
		{
			return _props?.GetStr(key, defaultValue) ?? defaultValue;
		}

		public int GetInt(string key, int defaultValue = 0)
		{
			return _props?.GetInt(key, defaultValue) ?? defaultValue;
		}

		public float GetFloat(string key, float defaultValue = 0f)
		{
			return _props?.GetFloat(key, defaultValue) ?? defaultValue;
		}

		public long GetLong(string key, long defaultValue = 0)
		{
			return _props?.GetLong(key, defaultValue) ?? defaultValue;
		}

		public bool GetBool(string key)
		{
			return _props?.GetBool(key) ?? false;
		}

		public void SetStr(string key, string value)
		{
			_props?.SetStr(key, value);
		}

		public void SetInt(string key, int value)
		{
			_props?.SetInt(key, value);
		}

		public void SetFloat(string key, float value)
		{
			_props?.SetFloat(key, value);
		}

		public void SetLong(string key, long value)
		{
			_props?.SetLong(key, value);
		}

		public void SetBool(string key, bool value)
		{
			_props?.SetBool(key, value);
		}
	}
}
