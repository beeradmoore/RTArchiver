using System.Text.Json.Serialization;

namespace RTArchiver.Data;

public class Genre
{
	[JsonPropertyName("id")]
	public int Id { get; set; }

	[JsonPropertyName("type")]
	public string Type { get; set; } = string.Empty;

	[JsonPropertyName("attributes")]
	public Genre_Attributes Attributes { get; set; } = new Genre_Attributes();

	[JsonPropertyName("links")]
	public Dictionary<string, string> Links { get; set; } = new Dictionary<string, string>();

	[JsonPropertyName("uuid")]
	public string Uuid { get; set; } = string.Empty;

	// Doesn't seem to have any data.
	//[JsonPropertyName("included")]
	//public Genere_Included Included { get; set; }

	[JsonIgnore]
	public string Name => Attributes?.Name ?? string.Empty;

	[JsonIgnore]
	public string Slug => Attributes?.Slug ?? string.Empty;
}

public class Genre_Attributes
{
	[JsonPropertyName("name")]
	public string Name { get; set; } = string.Empty;

	[JsonPropertyName("slug")]
	public string Slug { get; set; } = string.Empty;
}