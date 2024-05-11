using System.Text.Json.Serialization;

namespace RTArchiver.Data;

public class CommunityUser
{
	[JsonPropertyName("id")]
	public string Id { get; set; } = string.Empty;

	[JsonPropertyName("name")]
	public string Name { get; set; } = string.Empty;

	[JsonPropertyName("member_tier")]
	public string MemberTier { get; set; } = string.Empty;

	[JsonPropertyName("roles")]
	public string[] Roles { get; set; } = [];

	[JsonPropertyName("badges")]
	public string[] Badges { get; set; } = [];

	[JsonPropertyName("display_title")]
	public string DisplayTitle { get; set; } = string.Empty;

	[JsonPropertyName("profile_picture")]
	public string ProfilePicture { get; set; } = string.Empty;

	[JsonPropertyName("muted")]
	public bool Muted { get; set; } = false;

	[JsonPropertyName("metadata")]
	public CommunityUser_Metadata Metadata { get; set; } = new CommunityUser_Metadata();

	//[JsonPropertyName("posts")]
	//public object[] posts { get; set; }
}

public class CommunityUser_Metadata
{
	[JsonPropertyName("muted")]
	public bool Muted { get; set; }

	[JsonPropertyName("followed")]
	public bool Followed { get; set; }

	[JsonPropertyName("following_count")]
	public int FollowingCount { get; set; }

	[JsonPropertyName("followers_count")]
	public int FollowersCount { get; set; }

	[JsonPropertyName("following")]
	public bool Following { get; set; }
}

