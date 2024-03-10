using System.Text.Json.Serialization;

namespace RTArchiver.Data;

public class Genre
{
	[JsonPropertyName("id")]
	public int Id { get; set; }
	
	[JsonPropertyName("type")]
	public string Type { get; set; }
	
	// public Attributes attributes { get; set; }
	
	//public Links links { get; set; }
	
	[JsonPropertyName("uuid")]
	public string Uuid { get; set; }
	
	//public Included included { get; set; }
}