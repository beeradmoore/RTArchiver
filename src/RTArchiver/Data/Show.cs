using System.Text.Json.Serialization;

namespace RTArchiver.Data;

public class Show
{
	[JsonPropertyName("id")]
	public int Id { get; set; }

	[JsonPropertyName("type")]
	public string Type { get; set; } = string.Empty;

	[JsonPropertyName("attributes")]
	public Show_Attributes Attributes { get; set; } = new Show_Attributes();

	[JsonPropertyName("links")]
	public Show_Links Links { get; set; } = new Show_Links();

	[JsonPropertyName("uuid")]
	public string Uuid { get; set; } = string.Empty;

	[JsonPropertyName("canonical_links")]
	public Show_CanonicaLinks CanonicalLinks { get; set; } = new Show_CanonicaLinks();

	[JsonPropertyName("included")]
	public Show_Included Included { get; set; } = new Show_Included();
	
	[JsonIgnore]
	public string Title => Attributes?.Title ?? string.Empty;

	[JsonIgnore]
	public string Slug => Attributes?.Slug ?? string.Empty;
}

public class Show_Links
{
	[JsonPropertyName("self")]
	public string Self { get; set; } = string.Empty;
	
	[JsonPropertyName("seasons")]
	public string Seasons { get; set; } = string.Empty;
	
	[JsonPropertyName("bonus_features")]
	public string BonusFeatures { get; set; } = string.Empty;
	
	[JsonPropertyName("related")]
	public string Related { get; set; } = string.Empty;

	[JsonPropertyName("product_collections")]
	public string ProductCollections { get; set; } = string.Empty;
	
	[JsonPropertyName("rich_card_reference_url")]
	public string RichCardReferenceUrl { get; set; } = string.Empty;
}

public class Show_Attributes
{
	[JsonPropertyName("title")]
    public string Title { get; set; }
    
	//public object rating { get; set; }
    
	[JsonPropertyName("slug")]
    public string Slug { get; set; }
    
    //public Search_conversions search_conversions { get; set; }

    [JsonPropertyName("genres")]
    public List<string> Genres { get; set; } = new List<string>();
    
    [JsonPropertyName("is_sponsors_only")]
    public bool IsSponsorsOnly { get; set; }
    
    [JsonPropertyName("published_at")]
    public string PublishedAt { get; set; }
    
    [JsonPropertyName("summary")]
    public string Summary { get; set; }
    
    [JsonPropertyName("supporting_cast_url")]
    public string SupportingCastUrl { get; set; }
    
    [JsonPropertyName("category")]
    public string Category { get; set; }
    
    [JsonPropertyName("channel_id")]
    public string ChannelId { get; set; }
    
    [JsonPropertyName("channel_slug")]
    public string ChannelSlug { get; set; }
    
    [JsonPropertyName("season_count")]
    public int SeasonCount { get; set; }
    
    [JsonPropertyName("episode_count")]
    public int EpisodeCount { get; set; }
    
    [JsonPropertyName("last_episode_golive_at")]
    public string LastEpisodeGoLiveAt { get; set; }
    
    [JsonPropertyName("season_order")]
    public string SeasonOrder { get; set; }
    
    [JsonPropertyName("episode_order")]
    public string EpisodeOrder { get; set; }
    
    [JsonPropertyName("trending_score")]
    public double TrendingScore { get; set; }
    
    //public object[] schedule { get; set; }
    
    //public object schedule_expires_at { get; set; }
}

public class Show_Included()
{
	[JsonPropertyName("images")]
	public List<Image> Images { get; set; } = new List<Image>();
}

public class Show_CanonicaLinks
{
	[JsonPropertyName("self")]
	public string Self { get; set; } = string.Empty;
	
	[JsonPropertyName("s1e1")]
	public string S1E1 { get; set; } = string.Empty;
}
