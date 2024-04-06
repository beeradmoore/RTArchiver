using System.Xml.Serialization;
using SQLite;

namespace RTArchiver.Data;



[XmlRoot("sitemapindex", Namespace="http://www.sitemaps.org/schemas/sitemap/0.9")]
public class SitemapIndex
{
	[XmlElement("sitemap")]
	public Sitemap[] Sitemaps { get; set; }
}


[Table("sitemap")]
[XmlRoot("sitemap")]
public class Sitemap
{
	[PrimaryKey]
	[Column("id")]
	[XmlIgnore]
	public int Id { get; set; }
	
	[Column("tag")]
	[Indexed]
	[XmlIgnore]
	public string Tag { get; set; } = string.Empty;
	
	[Unique]
	[Column("loc")]
	[XmlElement("loc")]
	public string Location { get; set; } = string.Empty;

	[Column("lastmod")]
	[XmlElement("lastmod")]
	public DateTime LastModified { get; set; } = DateTime.MinValue;

	public static string GetTagFromLocation(string location)
	{
		return location switch
		{
			"https://svod-be.roosterteeth.com/sitemaps/static_pages.xml" => "static-pages",
			"https://svod-be.roosterteeth.com/sitemaps/rooster-teeth/videos.xml" => "rooster-teeth",
			"https://svod-be.roosterteeth.com/sitemaps/achievement-hunter/videos.xml" => "achievement-hunter",
			"https://svod-be.roosterteeth.com/sitemaps/funhaus/videos.xml" => "funhaus",
			"https://svod-be.roosterteeth.com/sitemaps/death-battle/videos.xml" => "death-battle",
			"https://svod-be.roosterteeth.com/sitemaps/kinda-funny/videos.xml" => "kinda-funny",
			"https://svod-be.roosterteeth.com/sitemaps/friends-of-rt/videos.xml" => "friends-of-rt",
			"https://svod-be.roosterteeth.com/sitemaps/rwby-universe/videos.xml" => "rwby-universe",
			"https://svod-be.roosterteeth.com/sitemaps/red-vs-blue-universe/videos.xml" => "red-vs-blue-universe",
			"https://svod-be.roosterteeth.com/sitemaps/all-good-no-worries/videos.xml" => "all-good-no-worries",
			"https://svod-be.roosterteeth.com/sitemaps/best-friends-today/videos.xml" => "best-friends-today",
			"https://svod-be.roosterteeth.com/sitemaps/inside-gaming/videos.xml" => "inside-gaming",
			"https://svod-be.roosterteeth.com/sitemaps/tales-from-the-stinky-dragon/videos.xml" => "tales-from-the-stinky-dragon",
			"https://svod-be.roosterteeth.com/sitemaps/dogbark/videos.xml" => "dogbark",
			"https://svod-be.roosterteeth.com/sitemaps/f-kface/videos.xml" => "f-kface",
			"https://svod-be.roosterteeth.com/sitemaps/camp-camp/videos.xml" => "camp-camp",
			"https://svod-be.roosterteeth.com/sitemaps/red-web/videos.xml" => "red-web",
			_ => throw new Exception($"Unexpected location ({location}) found."),
		};
	}
	
	public static int GetIdFromLocation(string location)
	{
		return location switch
		{
			"https://svod-be.roosterteeth.com/sitemaps/static_pages.xml" => 1,
			"https://svod-be.roosterteeth.com/sitemaps/rooster-teeth/videos.xml" => 2,
			"https://svod-be.roosterteeth.com/sitemaps/achievement-hunter/videos.xml" => 3,
			"https://svod-be.roosterteeth.com/sitemaps/funhaus/videos.xml" => 4,
			"https://svod-be.roosterteeth.com/sitemaps/death-battle/videos.xml" => 5,
			"https://svod-be.roosterteeth.com/sitemaps/kinda-funny/videos.xml" => 6,
			"https://svod-be.roosterteeth.com/sitemaps/friends-of-rt/videos.xml" => 7,
			"https://svod-be.roosterteeth.com/sitemaps/rwby-universe/videos.xml" => 8,
			"https://svod-be.roosterteeth.com/sitemaps/red-vs-blue-universe/videos.xml" => 9,
			"https://svod-be.roosterteeth.com/sitemaps/all-good-no-worries/videos.xml" => 10,
			"https://svod-be.roosterteeth.com/sitemaps/best-friends-today/videos.xml" => 11,
			"https://svod-be.roosterteeth.com/sitemaps/inside-gaming/videos.xml" => 12,
			"https://svod-be.roosterteeth.com/sitemaps/tales-from-the-stinky-dragon/videos.xml" => 13,
			"https://svod-be.roosterteeth.com/sitemaps/dogbark/videos.xml" => 14,
			"https://svod-be.roosterteeth.com/sitemaps/f-kface/videos.xml" => 15,
			"https://svod-be.roosterteeth.com/sitemaps/camp-camp/videos.xml" => 16,
			"https://svod-be.roosterteeth.com/sitemaps/red-web/videos.xml" => 17,
			_ => throw new Exception($"Unexpected location ({location}) found."),
		};
	}
}


public abstract class BaseSet<T>
{
	[XmlElement("url")]
	public List<T> Urls { get; set; }
}

[XmlRoot("urlset", Namespace="http://www.sitemaps.org/schemas/sitemap/0.9")]
public class StaticPageSet : BaseSet<StaticPage>
{
	
}

[SQLite.Table("static_page")]
public class StaticPage : Url
{
	
}

public class Url
{
	[Unique]
	[Column("guid")]
	[XmlIgnore]
	public byte[] Guid { get; set; }
	
	[PrimaryKey]
	[Column("loc")]
	[XmlElement("loc")]
	public string Location { get; set; } = string.Empty;
	
	[Column("changefreq")]
	[XmlElement("changefreq")]
	public string ChangeFrequency { get; set; } = string.Empty;
	
	[Column("priority")]
	[XmlElement("priority")]
	public double Priority { get; set; } = -1.0;
}

[XmlRoot("urlset", Namespace="http://www.sitemaps.org/schemas/sitemap/0.9")]
public class VideoSet : BaseSet<VideoUrl>
{
	
}

[SQLite.Table("video_details")]
public class VideoUrl : Url
{
	[Ignore]
	[XmlElement("video", Namespace = "http://www.google.com/schemas/sitemap-video/1.1")]
	public SiteMapVideo Video { get; set; }

	[XmlIgnore]
	[Indexed]
	public int SitemapId { get; set; } = -1;

	[XmlIgnore]
	[Indexed]
	public string SitemapTag { get; set; } = string.Empty;
}

[Table("video")]
public class SiteMapVideo
{
	[Unique]
	[Column("guid")]
	[XmlIgnore]
	public byte[] Guid { get; set; }
	
	[XmlElement("thumbnail_loc")]
	public string ThumbnailLocation { get; set; } = string.Empty;
	
	[XmlElement("title")]
	public string Title { get; set; } = string.Empty;
	
	[XmlElement("description")]
	public string Description { get; set; } = string.Empty;
	
	[XmlElement("player_loc")]
	public string PlayerLocation { get; set; } = string.Empty;

	[XmlElement("duration")]
	public int Duration { get; set; } = -1;
	
	[XmlElement("publication_date")]
	public DateTime PublicationDate { get; set; } = DateTime.MinValue;

	[XmlElement("requires_subscription")]
	public string RequiresSubscription { get; set; } = string.Empty;

	[XmlIgnore]
	public bool? RequiresSubscriptionBool
	{
		get
		{
			if (RequiresSubscription == "no")
			{
				return false;
			}
			
			if (RequiresSubscription == "yes")
			{
				return true;
			}

			return null;
		}
	}
}