using SQLite;

namespace RTArchiver.Data;

[Table("comment_cache_item")]
public class CommentCacheItem
{
	[PrimaryKey]
	[Column("url")]
	public string Url { get; set; } = string.Empty;
	
	[Column("etag")]
	public string ETag { get; set; } = string.Empty;

	[Column("last_checked")]
	public DateTime LastChecked { get; set; } = DateTime.MinValue;
}