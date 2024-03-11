using System.Text.Json.Serialization;

namespace RTArchiver.Data;

public class Episode
{
	[JsonPropertyName("id")]
    public int Id { get; set; }

	[JsonPropertyName("type")]
	public string Type { get; set; } = string.Empty;

	[JsonPropertyName("attributes")]
	public Episode_Attributes Attributes { get; set; } = new Episode_Attributes();

	[JsonPropertyName("links")]
	public Episode_Links Links { get; set; } = new Episode_Links();

	[JsonPropertyName("uuid")]
	public string Uuid { get; set; } = string.Empty;

	[JsonPropertyName("included")]
	public Episode_Included Included { get; set; } = new Episode_Included();

	//[JsonPropertyName("canonical_links")]
	//public Canonical_links canonical_links { get; set; }
	
}

public class Episode_Attributes
{
	[JsonPropertyName("title")]
	public string Title { get; set; } = string.Empty;
    
	[JsonPropertyName("slug")]
    public string Slug { get; set; } = string.Empty;
    
	//[JsonPropertyName("rating")]
	//public object rating { get; set; }
    
	[JsonPropertyName("caption")]
    public string Caption { get; set; } = string.Empty;
    
	[JsonPropertyName("number")]
    public int Number { get; set; }
    
	[JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
    
	[JsonPropertyName("display_title")]
    public string DisplayTitle { get; set; } = string.Empty;
    
	[JsonPropertyName("length")]
    public int Length { get; set; }
    
	[JsonPropertyName("advert_config")]
    public string AdvertConfig { get; set; } = string.Empty;
    
	[JsonPropertyName("advertising")]
    public bool Advertising { get; set; }
    
	[JsonPropertyName("ad_timestamps")]
    public string AdTimestamps { get; set; } = string.Empty;
    
	[JsonPropertyName("public_golive_at")]
    public string PublicGoLiveAt { get; set; } = string.Empty;
    
	[JsonPropertyName("sponsor_golive_at")]
    public string SponsorGoLiveAt { get; set; } = string.Empty;
    
	[JsonPropertyName("member_golive_at")]
    public string MemberGoLiveAt { get; set; } = string.Empty;
    
    [JsonPropertyName("original_air_date")]
    public string OriginalAirDate { get; set; } = string.Empty;
    
    [JsonPropertyName("channel_id")]
    public string ChannelId { get; set; } = string.Empty;
    
    [JsonPropertyName("channel_slug")]
    public string ChannelSlug { get; set; } = string.Empty;
    
    [JsonPropertyName("season_id")]
    public string SeasonId { get; set; } = string.Empty;
    
    [JsonPropertyName("season_slug")]
    public string SeasonSlug { get; set; } = string.Empty;
    
    [JsonPropertyName("season_number")]
    public int SeasonNumber { get; set; }
    
    [JsonPropertyName("show_title")]
    public string ShowTitle { get; set; } = string.Empty;
    
    [JsonPropertyName("show_id")]
    public string ShowId { get; set; } = string.Empty;
    
    [JsonPropertyName("show_slug")]
    public string ShowSlug { get; set; } = string.Empty;
    
    [JsonPropertyName("is_sponsors_only")]
    public bool IsSponsorsOnly { get; set; }
    
    [JsonPropertyName("member_tier_i")]
    public int MemberTierI { get; set; }
    
    [JsonPropertyName("sort_number")]
    public int SortNumber { get; set; }

    [JsonPropertyName("genres")]
    public List<string> Genres { get; set; } = new List<string>();
    
    [JsonPropertyName("is_live")]
    public bool IsLive { get; set; }
    
    [JsonPropertyName("is_schedulable")]
    public bool IsSchedulable { get; set; }
    
    [JsonPropertyName("season_order")]
    public string SeasonOrder { get; set; } = string.Empty;
    
    [JsonPropertyName("episode_order")]
    public string EpisodeOrder { get; set; } = string.Empty;
    
    [JsonPropertyName("downloadable")]
    public bool Downloadable { get; set; }

    [JsonPropertyName("blacklisted_countries")]
    public List<object> BlacklistedCountries { get; set; } = new List<object>();
    
    [JsonPropertyName("upsell_next")]
    public bool UpsellNext { get; set; }
    
    [JsonPropertyName("trending_score")]
    public double TrendingScore { get; set; }
    
    [JsonPropertyName("time_boost")]
    public double TimeBoost { get; set; }
    
    //[JsonPropertyName("credits_start_at")]
    //public object credits_start_at { get; set; }
    
    // This can be null
    //[JsonPropertyName("trending_carousel")]
    //public bool TrendingCarousel { get; set; }
}

public class Episode_Links
{
	[JsonPropertyName("self")]
    public string Self { get; set; } = string.Empty;
    
    [JsonPropertyName("show")]
    public string Show { get; set; } = string.Empty;
    
    [JsonPropertyName("related_shows")]
    public string RelatedShows { get; set; } = string.Empty;
    
    [JsonPropertyName("channel")]
    public string Channel { get; set; } = string.Empty;
    
    [JsonPropertyName("season")]
    public string Season { get; set; } = string.Empty;

    [JsonPropertyName("next")]
    public string Next { get; set; } = string.Empty;

    [JsonPropertyName("videos")]
    public string Videos { get; set; } = string.Empty;

    [JsonPropertyName("products")]
    public string Products { get; set; } = string.Empty;
}

public class Episode_Included
{
	[JsonPropertyName("images")]
	public List<Image> Images { get; set; } = new List<Image>();

	[JsonPropertyName("tags")]
	public List<object> Tags { get; set; } = new List<object>();

	[JsonPropertyName("cast_members")]
	public List<object> CastMembers { get; set; } = new List<object>();
}



