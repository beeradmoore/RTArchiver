using System.Text.Json.Serialization;

namespace RTArchiver.Data.Responses;

public abstract class BaseResponse<T>
{
	[JsonPropertyName("data")]
	public List<T> Data { get; set; } = new List<T>();

	[JsonPropertyName("page")]
	public int Page { get; set; }

	[JsonPropertyName("per_page")]
	public int PerPage { get; set; }

	[JsonPropertyName("total_pages")]
	public int TotalPages { get; set; }

	[JsonPropertyName("total_results")]
	public int TotalResults { get; set; }
}