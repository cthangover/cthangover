using System.Text.Json.Serialization;

namespace Cthangover.Core.Mods
{
	public class CatalogLocaleEntry
	{
		[JsonPropertyName("lang")]
		public string Lang { get; set; }

		[JsonPropertyName("name")]
		public string Name { get; set; }

		[JsonPropertyName("description")]
		public string Description { get; set; }
	}
}
