using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace RTArchiver.Data
{
	public class Season
	{
		[JsonPropertyName("attributes")]
		public Season_Attributes Attributes { get; set; } = new Season_Attributes();

		[JsonPropertyName("id")]
		public int Id { get; set; }

		[JsonPropertyName("included")]
		public Season_Included Included { get; set; } = new Season_Included();

		[JsonPropertyName("links")]
		public Season_Links Links { get; set; } = new Season_Links();

		[JsonPropertyName("type")]
		public string Type { get; set; } = string.Empty;

		[JsonPropertyName("uuid")]
		public string Uuid { get; set; } = string.Empty;
	}

	public class  Season_Attributes
	{
		[JsonPropertyName("description")]
		public string Description { get; set; } = string.Empty;

		[JsonPropertyName("episode_count")]
		public int EpisodeCount { get; set; } = 0;

		[JsonPropertyName("episodes_available")]
		public Season_Attributes_EpisodesAvailable EpisodesAvailable { get; set; } = new Season_Attributes_EpisodesAvailable();

		[JsonPropertyName("number")]
		public int Number { get; set; }

		[JsonPropertyName("published_at")]
		public DateTime PublishedAt { get; set; }

		[JsonPropertyName("show_id")]
		public string ShowId { get; set; } = string.Empty;

		[JsonPropertyName("show_slug")]
		public string ShowSlug { get; set; } = string.Empty;

		[JsonPropertyName("slug")]
		public string Slug { get; set; } = string.Empty;

		[JsonPropertyName("title")]
		public string Title { get; set; } = string.Empty;
	}

	public class Season_Attributes_EpisodesAvailable
	{
		[JsonPropertyName("member")]
		public bool Member { get; set; } = false;

		[JsonPropertyName("public")]
		public bool Public { get; set; } = false;

		[JsonPropertyName("sponsor")]
		public bool Sponsor { get; set; } = false;
	}

	public class Season_Included
	{
		[JsonPropertyName("images")]
		public List<Image> Images { get; set; } = new List<Image>();
	}

	public class Season_Links
	{
		[JsonPropertyName("episodes")]
		public string Episodes { get; set; } = string.Empty;

		[JsonPropertyName("self")]
		public string Self { get; set; } = string.Empty;
	}
}
