using System.Text.Json.Serialization;

namespace RTArchiver.Data;

public class Video
{
	[JsonPropertyName("id")]
	public int Id { get; set; }

	[JsonPropertyName("uuid")]
	public string Uuid { get; set; } = string.Empty;

	[JsonPropertyName("links")]
	public Video_Links Links { get; set; } = new Video_Links();
	
	[JsonPropertyName("type")]
	public string Type { get; set; } = string.Empty;

	[JsonPropertyName("attributes")]
	public Video_Attributes Attributes { get; set; } = new Video_Attributes();
	
	//[JsonPropertyName("included")]
	//public Video_Included included { get; set; }
}



public class Video_Links
{
	[JsonPropertyName("self")]
	public string Self { get; set; } = string.Empty;
    
	[JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
    
	[JsonPropertyName("download")]
    public string Download { get; set; } = string.Empty;
}

public class Video_Attributes
{
	[JsonPropertyName("encoding_pipeline")]
    public string EncodingPipeline { get; set; } = string.Empty;
    
	[JsonPropertyName("uid")]
    public string Uid { get; set; } = string.Empty;
    
	[JsonPropertyName("frame_sizes")]
    public object FrameSizes { get; set; } = string.Empty;
    
	[JsonPropertyName("intro_starts_at")]
    public object IntroStartsAt { get; set; } = string.Empty;
    
	[JsonPropertyName("intro_stops_at")]
    public object IntroStopsAt { get; set; } = string.Empty;
    
	[JsonPropertyName("media_type")]
    public string MediaType { get; set; } = string.Empty;
    
	[JsonPropertyName("member_tier")]
    public string MemberTier { get; set; } = string.Empty;
    
	//[JsonPropertyName("bandwidth")]
    //public object? Bandwidth { get; set; }
    
	[JsonPropertyName("embed")]
    public bool Embed { get; set; }
    
	[JsonPropertyName("content_id")]
    public int ContentId { get; set; }
    
	[JsonPropertyName("content_slug")]
    public string ContentSlug { get; set; } = string.Empty;
    
	[JsonPropertyName("content_uuid")]
    public string ContentUuid { get; set; } = string.Empty;
    
	[JsonPropertyName("public_golive_at")]
    public string PublicGoLiveAt { get; set; } = string.Empty;
    
	[JsonPropertyName("sponsor_golive_at")]
    public string sponsor_golive_at { get; set; } = string.Empty;
    
	[JsonPropertyName("member_golive_at")]
    public string MemberGoLiveAt { get; set; } = string.Empty;
    
	[JsonPropertyName("is_sponsors_only")]
    public bool IsSponsorsOnly { get; set; }
    
	[JsonPropertyName("remote_asset_video")]
    public bool RemoteAssetVideo { get; set; }
    
	///[JsonPropertyName("image_pattern_url")]
    //public object ImagePatternUrl { get; set; }
    
	//[JsonPropertyName("bif_url")]
    //public object BifUrl { get; set; }

	[JsonPropertyName("url")]
	public string Url { get; set; } = string.Empty;

	//[JsonPropertyName("ad_breaks")]
	//public object[] AdBreaks { get; set; }
}

public class Video_Included
{

}

