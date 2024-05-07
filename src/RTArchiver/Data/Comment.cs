using System.Text.Json.Serialization;

namespace RTArchiver.Data;

public class Comment
{
	[JsonPropertyName("id")]
	public int Id { get; set; } = 0;
    
	[JsonPropertyName("uuid")]
    public string Uuid { get; set; } = string.Empty;

	[JsonPropertyName("topic_uuid")]
	public string TopicUuid { get; set; } = string.Empty;

	[JsonPropertyName("parent_uuid")]
	public string ParentUuid { get; set; } = string.Empty;

	[JsonPropertyName("owner_uuid")]
	public string OwnerUuid { get; set; } = string.Empty;

	[JsonPropertyName("message")]
	public string Message { get; set; } = string.Empty;

	[JsonPropertyName("created_at")]
	public string CreatedAt { get; set; } = string.Empty;

	[JsonPropertyName("updated_at")]
	public string UpdatedAt { get; set; } = string.Empty;

	[JsonPropertyName("edited_at")]
	public string EditedAt { get; set; } = string.Empty;

	[JsonPropertyName("topic_type")]
	public string TopicType { get; set; } = string.Empty;

    [JsonPropertyName("spoiler")]
    public bool Spoiler { get; set; } = false;

    [JsonPropertyName("likes_count")]
    public int LikesCount { get; set; } = 0;
    
    [JsonPropertyName("owner_roles")]
    public string[] OwnerRoles { get; set; } = [];
    
    [JsonPropertyName("owner_badges")]
    public string[] OwnerBadges { get; set; } = [];
    
    [JsonPropertyName("owner_name")]
    public string OwnerName { get; set; } = string.Empty;
    
    [JsonPropertyName("owner_status")]
    public string OwnerStatus { get; set; } = string.Empty;
    
    [JsonPropertyName("owner_image")]
    public string OwnerImage { get; set; } = string.Empty;

    [JsonPropertyName("child_comments_total_count")]
    public int ChildCommentsTotalCount { get; set; } = 0;
    
    [JsonPropertyName("staff_member_has_replied")]
    public bool StaffMemberHasReplied { get; set; } = false;

    [JsonPropertyName("created_by_staff")]
    public bool CreatedByStaff { get; set; } = false;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("liked_status")]
    public bool LikedStatus { get; set; } = false;
}