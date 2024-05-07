using System.Text.Json.Serialization;

namespace RTArchiver.Data.Responses;

public abstract class BaseResponse<T>
{
	[JsonPropertyName("data")]
	public List<T> Data { get; set; } = new List<T>();

	[JsonPropertyName("page")]
	[JsonConverter(typeof(JsonConverters.StringToIntConverter))]
	public int Page { get; set; }

	[JsonPropertyName("per_page")]
	[JsonConverter(typeof(JsonConverters.StringToIntConverter))]
	public int PerPage { get; set; }

	[JsonPropertyName("total_pages")]
	[JsonConverter(typeof(JsonConverters.StringToIntConverter))]
	public int TotalPages { get; set; }

	[JsonPropertyName("total_results")]
	[JsonConverter(typeof(JsonConverters.StringToIntConverter))]
	public int TotalResults { get; set; }

	[JsonIgnore]
	public int HttpStatusCode { get; set; } = 0;
}