using System.Text.Json.Serialization;

namespace RTArchiver.Data;

public class User
{
	[JsonPropertyName("id")]
	public string Id { get; set; } = string.Empty;

	[JsonPropertyName("type")]
	public string Type { get; set; } = string.Empty;

	[JsonPropertyName("attributes")]
	public UserAttributes Attributes { get; set; } = new UserAttributes();
}

public class UserAttributes
{
	[JsonPropertyName("uuid")]
	public string Uuid { get; set; } = string.Empty;

	[JsonPropertyName("created_at")]
	public string CreatedAt { get; set; } = string.Empty;

	[JsonPropertyName("username")]
	public string Username { get; set; } = string.Empty;

	[JsonPropertyName("display_title")]
	public string DisplayTitle { get; set; } = string.Empty;

	[JsonPropertyName("pictures")]
	public UserPictures Pictures { get; set; } = new UserPictures();

	[JsonPropertyName("member_tier")]
	public string MemberTier { get; set; } = string.Empty;

	[JsonPropertyName("member_tier_i")]
	public int MemberTierI { get; set; }

	[JsonPropertyName("used_trial")]
	public bool UsedTrial { get; set; }

	[JsonPropertyName("expires_at")]
	public string ExpiresAt { get; set; } = string.Empty;

	[JsonPropertyName("social_connected")]
	public string[] SocialConnected { get; set; } = [];

	[JsonPropertyName("badges")]
	public string[] Badges { get; set; } = [];

	[JsonPropertyName("about")]
	public string About { get; set; } = string.Empty;

	[JsonPropertyName("location")]
	public string Location { get; set; } = string.Empty;

	[JsonPropertyName("timezone")]
	public string Timezone { get; set; } = string.Empty;

	[JsonPropertyName("store_region")]
	public string StoreRegion { get; set; } = string.Empty;

	[JsonPropertyName("banned_until")]
	public string BannedUntil { get; set; } = string.Empty;

	[JsonPropertyName("is_first_plus")]
	public bool IsFirstPlus { get; set; }
}

public class UserPictures
{
	[JsonPropertyName("tb")]
	public UserPicture Thumbnail { get; set; } = new UserPicture();

	[JsonPropertyName("sm")]
	public UserPicture Small { get; set; } = new UserPicture();

	[JsonPropertyName("md")]
	public UserPicture Medium { get; set; } = new UserPicture();

	[JsonPropertyName("original")]
	public UserPicture Original { get; set; } = new UserPicture();
}

public class UserPicture
{
	[JsonPropertyName("profile")]
	public string Profile { get; set; } = string.Empty;

	[JsonPropertyName("cover")]
	public string Cover { get; set; } = string.Empty;
}

