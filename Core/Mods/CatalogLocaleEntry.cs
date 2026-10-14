using System.Text.Json.Serialization;

namespace Cthangover.Core.Mods
{
	/// <summary>
	/// One localized name/description entry of a catalog mod or collection
	/// (the <c>locale[]</c> array). Consumers pick the best entry for the
	/// player's language, falling back to the "en" entry and then to the
	/// flat top-level fields.
	/// </summary>
	public class CatalogLocaleEntry
	{
		/// <summary>Language code (ISO 639-1, e.g. "en", "ru").</summary>
		[JsonPropertyName("lang")]
		public string Lang { get; set; }

		/// <summary>Localized display name.</summary>
		[JsonPropertyName("name")]
		public string Name { get; set; }

		/// <summary>Localized short description.</summary>
		[JsonPropertyName("description")]
		public string Description { get; set; }
	}
}
