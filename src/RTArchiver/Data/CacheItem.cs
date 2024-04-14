using System.Net.Http.Headers;
using SQLite;

namespace RTArchiver.Data;


[Table("cache_item")]
public class CacheItem
{
	[PrimaryKey]
	[Column("endpoint")]
	public string Endpoint { get; set; } = string.Empty;
	
	[Column("etag")]
	public string ETag { get; set; } = string.Empty;

	[Column("last_checked")]
	public DateTime LastChecked { get; set; } = DateTime.MinValue;
}