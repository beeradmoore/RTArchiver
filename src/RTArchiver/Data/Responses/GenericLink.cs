using System.Text.Json.Serialization;

namespace RTArchiver.Data.Responses;

public class GenericLink
{
	[JsonPropertyName("links")]
	public Dictionary<string, object?> Links { get; set; } = new Dictionary<string, object?>();
}