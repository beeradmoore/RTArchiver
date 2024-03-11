using System.Text.Json.Serialization;

namespace RTArchiver.Data;

public class BaseDataResponse<T>
{
	[JsonPropertyName("data")]
	public T[] Data { get; set; }
	
	[JsonPropertyName("page")]
	public int Page { get; set; }
	
	[JsonPropertyName("per_page")]
	public int PerPage { get; set; }
	
	[JsonPropertyName("total_pages")]
	public int TotalPages { get; set; }

	[JsonPropertyName("total_results")]
	public int TotalResults { get; set; }
}