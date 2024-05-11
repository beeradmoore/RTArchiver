using System.Text.Json.Serialization;

namespace RTArchiver.Data.Responses;

public class CommunityUserResponse 
{
	[JsonPropertyName("data")]
	public CommunityUser Data { get; set; } = new CommunityUser();
}