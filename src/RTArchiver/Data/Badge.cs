using System.Text.Json.Serialization;

namespace RTArchiver.Data;

public class Badge
{
	[JsonPropertyName("id")]
	public int Id { get; set; } = 0;

	[JsonPropertyName("label")]
	public string Label { get; set; } = string.Empty;
    
	[JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
    
	[JsonPropertyName("cdn_url")]
    public string CdnUrl { get; set; } = string.Empty;
}