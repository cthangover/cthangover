using Cthangover.Core.Characters;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Battle.Actions
{
	/// <summary>
	/// Godot bridge for <see cref="Character"/> data. Flattens the four
	/// core battle stats (Health, Defence, Attack, Point) into
	/// Value/Max pairs for GDScript battle cores. All other character
	/// properties (any mod-defined extras) are accessible through the typed
	/// GetXxx/SetXxx methods backed by <see cref="PropertyData"/>,
	/// plus the raw <see cref="Properties"/> dictionary for iteration.
	/// This mirrors <see cref="GDActionCharacter"/> so that mods use
	/// the same API to read character stats and action parameters.
	/// </summary>
	/// <remarks>
	/// Constructed via <see cref="From"/> which copies attribute values
	/// from a <see cref="Character"/> — the resulting GDCharacter is
	/// a snapshot, not a live binding. Writes through SetXxx go to the
	/// backing <see cref="PropertyData"/> and trigger interceptor chains.
	/// </remarks>
	[GlobalClass]
	public partial class GDCharacter : RefCounted
	{
		/// <summary>Factory key matching the character's JSON ID.</summary>
		public string Id;
		/// <summary>Localised display name.</summary>
		public string Name;
		/// <summary>Current experience level.</summary>
		public int Level;
		/// <summary>Accumulated experience points.</summary>
		public int Exp;
		/// <summary>Recruitment chance percentage (0–100) after defeat.</summary>
		public int RecruitmentChance;

		/// <summary>Current health value.</summary>
		public int HealthValue;
		/// <summary>Maximum health (base value).</summary>
		public int HealthMax;
		/// <summary>Current defence value.</summary>
		public int DefenceValue;
		/// <summary>Maximum defence value.</summary>
		public int DefenceMax;
		/// <summary>Current attack power.</summary>
		public int AttackValue;
		/// <summary>Maximum attack power.</summary>
		public int AttackMax;
		/// <summary>Current action points remaining.</summary>
		public int PointValue;
		/// <summary>Maximum action points (reset to this each turn).</summary>
		public int PointMax;

		/// <summary>
		/// Raw string-to-string snapshot of the character's dynamic
		/// properties. Use for iteration and debugging; prefer the
		/// typed <c>GetXxx</c>/<c>SetXxx</c> methods for business logic.
		/// Mods can read arbitrary stats through <c>Properties["Speed"]</c>
		/// without type safety — use <c>GetFloat("Speed")</c> for a
		/// parsed value with fallback.
		/// </summary>
		public Godot.Collections.Dictionary<string, string> Properties;

		private PropertyData _props;

		/// <summary>
		/// Creates a GDScript-compatible snapshot from a
		/// <see cref="Character"/> runtime model. Core battle stats
		/// are flattened into Value/Max fields; dynamic properties
		/// are captured by reference so that SetXxx writes back to
		/// the character's <see cref="CharacterAttributes.Properties"/>.
		/// Returns an empty GDCharacter for null input.
		/// </summary>
		public static GDCharacter From(Character character)
		{
			var gd = new GDCharacter();
			if (character == null)
				return gd;

			gd.Id = character.ID;
			gd.Name = character.Name;
			gd.Level = character.Level;
			gd.Exp = character.Exp;
			gd.RecruitmentChance = character.RecruitmentChance;

			var attrs = character.Attributes;
			if (attrs != null)
			{
				gd._props = attrs.Properties;

				gd.HealthValue = attrs.Health?.Value ?? 0;
				gd.HealthMax = attrs.Health?.BaseValue ?? 0;
				gd.DefenceValue = attrs.Defence?.Value ?? 0;
				gd.DefenceMax = attrs.Defence?.BaseValue ?? 0;
				gd.AttackValue = attrs.Attack?.Value ?? 0;
				gd.AttackMax = attrs.Attack?.BaseValue ?? 0;
				gd.PointValue = attrs.Point?.Value ?? 0;
				gd.PointMax = attrs.Point?.BaseValue ?? 0;

				if (gd._props?.Values != null)
				{
					gd.Properties = new Godot.Collections.Dictionary<string, string>();
					foreach (var kvp in gd._props.Values)
						gd.Properties[kvp.Key] = kvp.Value;
				}
			}

			return gd;
		}

		/// <summary>
		/// Reads a dynamic property as a raw string. Returns
		/// <paramref name="defaultValue"/> (null) when the key is
		/// absent or <see cref="_props"/> is not initialised.
		/// </summary>
		public string GetStr(string key, string defaultValue = null)
		{
			return _props?.GetStr(key, defaultValue) ?? defaultValue;
		}

		/// <summary>
		/// Reads a dynamic property as a 32-bit integer. Returns
		/// <paramref name="defaultValue"/> (0) when the key is absent,
		/// the value is blank, or <see cref="_props"/> is not initialised.
		/// </summary>
		public int GetInt(string key, int defaultValue = 0)
		{
			return _props?.GetInt(key, defaultValue) ?? defaultValue;
		}

		/// <summary>
		/// Reads a dynamic property as a single-precision float using
		/// invariant culture (<c>"."</c> decimal). Returns
		/// <paramref name="defaultValue"/> (0f) when the key is absent
		/// or the value is unparseable.
		/// </summary>
		public float GetFloat(string key, float defaultValue = 0f)
		{
			return _props?.GetFloat(key, defaultValue) ?? defaultValue;
		}

		/// <summary>
		/// Reads a dynamic property as a 64-bit integer. Returns
		/// <paramref name="defaultValue"/> (0) on missing key or
		/// unparseable value.
		/// </summary>
		public long GetLong(string key, long defaultValue = 0)
		{
			return _props?.GetLong(key, defaultValue) ?? defaultValue;
		}

		/// <summary>
		/// Reads a dynamic property as a boolean. Always returns
		/// <c>false</c> when the key is absent, the value is blank,
		/// or <see cref="_props"/> is not initialised — there is no
		/// custom default because <c>false</c> is the conventional
		/// default for boolean flags.
		/// </summary>
		public bool GetBool(string key)
		{
			return _props?.GetBool(key) ?? false;
		}

		/// <summary>
		/// Writes a raw string to a dynamic property. Triggers
		/// any registered <see cref="IPropertyInterceptor"/> chain
		/// for this property before the value is stored.
		/// No-op when <see cref="_props"/> is not initialised.
		/// </summary>
		public void SetStr(string key, string value)
		{
			_props?.SetStr(key, value);
		}

		/// <summary>
		/// Writes an integer to a dynamic property. The value is
		/// converted to its default string representation, then
		/// routed through <see cref="SetStr"/> for interceptor
		/// processing. No-op when <see cref="_props"/> is null.
		/// </summary>
		public void SetInt(string key, int value)
		{
			_props?.SetInt(key, value);
		}

		/// <summary>
		/// Writes a float to a dynamic property using invariant
		/// culture (<c>"."</c> decimal). Routed through
		/// <see cref="SetStr"/> for interceptor processing.
		/// No-op when <see cref="_props"/> is null.
		/// </summary>
		public void SetFloat(string key, float value)
		{
			_props?.SetFloat(key, value);
		}

		/// <summary>
		/// Writes a 64-bit integer to a dynamic property. Routed
		/// through <see cref="SetStr"/> for interceptor processing.
		/// No-op when <see cref="_props"/> is null.
		/// </summary>
		public void SetLong(string key, long value)
		{
			_props?.SetLong(key, value);
		}

		/// <summary>
		/// Writes a boolean to a dynamic property as <c>"True"</c>
		/// or <c>"False"</c>. Routed through <see cref="SetStr"/>
		/// for interceptor processing. No-op when
		/// <see cref="_props"/> is null.
		/// </summary>
		public void SetBool(string key, bool value)
		{
			_props?.SetBool(key, value);
		}
	}
}
