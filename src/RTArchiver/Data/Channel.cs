using System.Text.Json.Serialization;

namespace RTArchiver.Data
{
	public class Channel
	{
		[JsonPropertyName("id")]
		public int Id { get; set; }

		[JsonPropertyName("attributes")]
		public Channel_Attributes Attributes { get; set; } = new Channel_Attributes();

		[JsonPropertyName("included")]
		public Channel_Included Included { get; set; } = new Channel_Included();

		[JsonPropertyName("links")]
		public Channel_Links Links { get; set; } = new Channel_Links();

		[JsonPropertyName("type")]
		public string Type { get; set; } = string.Empty;

		[JsonPropertyName("uuid")]
		public string Uuid { get; set; } = string.Empty;
	}

	public class Channel_Attributes
	{
		[JsonPropertyName("uuid")]
		public string BrandColor { get; set; } = string.Empty;

		[JsonPropertyName("carousel_logo_url")]
		public string CarouselLogoUrl { get; set; } = string.Empty;

		[JsonPropertyName("importance")]
		public int Importance { get; set; }

		[JsonPropertyName("name")]
		public string Name { get; set; } = string.Empty;

		[JsonPropertyName("site_description")]
		public string SiteDescription { get; set; } = string.Empty;

		[JsonPropertyName("slug")]
		public string Slug { get; set; } = string.Empty;

		[JsonPropertyName("vertical_logo_url")]
		public string VerticalLogoUrl { get; set; } = string.Empty;
	}

	public class Channel_Included
	{
		[JsonPropertyName("genres")]
		public List<Genre> Genres { get; set; } = new List<Genre>();

		[JsonPropertyName("images")]
		public List<Image> Images { get; set; } = new List<Image>();

	}

	public class Channel_Links
	{
		[JsonPropertyName("episodes")]
		public string Episodes { get; set; } = string.Empty;

		[JsonPropertyName("featured_items")]
		public string FeaturedItems { get; set; } = string.Empty;

		[JsonPropertyName("livestreams")]
		public string Livestreams { get; set; } = string.Empty;

		[JsonPropertyName("product_collections")]
		public string ProductCollections { get; set; } = string.Empty;

		[JsonPropertyName("self")]
		public string Self { get; set; } = string.Empty;

		[JsonPropertyName("shows")]
		public string Shows { get; set; } = string.Empty;
	}
}
