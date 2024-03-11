using System.Text.Json.Serialization;

namespace RTArchiver.Data;

public class BonusFeature
{
	[JsonPropertyName("id")]
    public int Id { get; set; }

	[JsonPropertyName("type")]
	public string Type { get; set; } = string.Empty;

	[JsonPropertyName("attributes")]
	public BonusFeature_Attributes Attributes { get; set; } = new BonusFeature_Attributes();

	[JsonPropertyName("links")]
	public BonusFeature_Links Links { get; set; } = new BonusFeature_Links();

	[JsonPropertyName("uuid")]
	public string Uuid { get; set; } = string.Empty;

	[JsonPropertyName("included")]
	public BonusFeature_Included Included { get; set; } = new BonusFeature_Included();

	//[JsonPropertyName("canonical_links")]
	//public Canonical_links CanonicalLinks { get; set; }
}

public class BonusFeature_Attributes
{
	[JsonPropertyName("title")]
    public string Title { get; set; }
    
	[JsonPropertyName("slug")]
    public string Slug { get; set; }
    
	//[JsonPropertyName("rating")]
    //public object Rating { get; set; }
    
	[JsonPropertyName("caption")]
    public string Caption { get; set; }
    
	[JsonPropertyName("number")]
    public int Number { get; set; }
    
	[JsonPropertyName("sort_number")]
    public int SortNumber { get; set; }
    
	[JsonPropertyName("description")]
    public string Description { get; set; }
    
	[JsonPropertyName("display_title")]
    public string DisplayTitle { get; set; }
    
	[JsonPropertyName("length")]
    public int Length { get; set; }
    
	[JsonPropertyName("advert_config")]
    public string AdvertConfig { get; set; }
    
	[JsonPropertyName("advertising")]
    public bool Advertising { get; set; }
    
	[JsonPropertyName("ad_timestamps")]
    public string AdTimestamps { get; set; }
    
	[JsonPropertyName("public_golive_at")]
    public string PublicGoLiveAt { get; set; }
    
	[JsonPropertyName("sponsor_golive_at")]
    public string SponsorGoLiveAt { get; set; }
    
	[JsonPropertyName("member_golive_at")]
    public string MemberGoLiveAt { get; set; }
    
	[JsonPropertyName("original_air_date")]
    public string OriginalAirDate { get; set; }
    
	[JsonPropertyName("channel_id")]
    public string ChannelId { get; set; }
    
	[JsonPropertyName("channel_slug")]
    public string ChannelSlug { get; set; }
    
	[JsonPropertyName("parent_content_id")]
    public string ParentContentId { get; set; }
    
	[JsonPropertyName("parent_content_type")]
    public string ParentContentType { get; set; }
    
	[JsonPropertyName("parent_content_slug")]
    public string ParentContentSlug { get; set; }
    
	[JsonPropertyName("parent_content_title")]
    public string ParentContentTitle { get; set; }
    
	[JsonPropertyName("show_id")]
    public string ShowId { get; set; }
    
	[JsonPropertyName("is_sponsors_only")]
    public bool IsSponsorsOnly { get; set; }
    
	[JsonPropertyName("member_tier_i")]
    public int MemberTierI { get; set; }
    
	[JsonPropertyName("is_live")]
    public bool IsLive { get; set; }
    
	[JsonPropertyName("is_schedulable")]
    public bool IsSchedulable { get; set; }
    
	[JsonPropertyName("downloadable")]
    public bool Downloadable { get; set; }
    
	//[JsonPropertyName("blacklisted_countries")]
    //public object[] blacklisted_countries { get; set; }
    
	[JsonPropertyName("trending_score")]
    public double TrendingScore { get; set; }
    
    //[JsonPropertyName("credits_start_at")]
	//public object CreditsStartAt { get; set; }
}

public class BonusFeature_Links
{
	[JsonPropertyName("self")]
    public string Self { get; set; } = string.Empty;
    
	[JsonPropertyName("channel")]
    public string Channel { get; set; } = string.Empty;
    
	[JsonPropertyName("parent_content")]
    public string ParentContent { get; set; } = string.Empty;
    
	[JsonPropertyName("products")]
    public string Products { get; set; } = string.Empty;
    
	[JsonPropertyName("videos")]
    public string Videos { get; set; } = string.Empty;

	[JsonPropertyName("next")]
	public string Next { get; set; } = string.Empty;
}

public class BonusFeature_Included
{
	public List<Image> Images { get; set; } = new List<Image>();
}


