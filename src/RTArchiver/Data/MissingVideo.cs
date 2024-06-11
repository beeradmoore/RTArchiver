using System.Text.Json.Serialization;

namespace RTArchiver.Data;

public class MissingVideo
{
	[JsonPropertyName("video_id")]
	public string VideoId { get; set; } = string.Empty;
	
	[JsonPropertyName("page_url")]
	public string PageUrl { get; set; } = string.Empty;
	
	[JsonPropertyName("title")]
	public string Title { get; set; } = string.Empty;
	
	[JsonPropertyName("display_title")]
	public string DisplayTitle { get; set; } = string.Empty;
	
	[JsonPropertyName("show_title")]
	public string ShowTitle { get; set; } = string.Empty;
	
	[JsonPropertyName("channel_slug")]
	public string ChannelSlug { get; set; } = string.Empty;
	
	[JsonPropertyName("show_slug")]
	public string ShowSlug { get; set; } = string.Empty;
	
	[JsonPropertyName("episode_slug")]
	public string EpisodeSlug { get; set; } = string.Empty;
	
	[JsonPropertyName("parent_type")]
	public string ParentType { get; set; } = string.Empty;
	
	[JsonPropertyName("parent_title")]
	public string ParentTitle { get; set; } = string.Empty;
	
	[JsonPropertyName("parent_slug")]
	public string ParentSlug { get; set; } = string.Empty;
	
	[JsonPropertyName("original_air_date")]
	public string OriginalAirDate { get; set; } = string.Empty;
	
	[JsonPropertyName("length")]
	public int Length { get; set; }
	
	[JsonPropertyName("stream_length")]
	public double StreamLength { get; set; }

	[JsonPropertyName("measured_length")]
	public double MeasuredLength { get; set; }
	
	[JsonPropertyName("download_urls")]
	public List<string> DownloadUrls { get; set; } = new List<string>();
	
	[JsonPropertyName("video_exists")]
	public bool VideoExists { get; set; }
	
}