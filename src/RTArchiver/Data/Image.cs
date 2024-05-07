using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace RTArchiver.Data
{
	public class Image : IEquatable<Image>
	{
		[JsonPropertyName("attributes")]
		public Image_Attributes Attributes { get; set; } = new Image_Attributes();

		[JsonPropertyName("id")]
		public int Id { get; set; }

		// Appears to be empty for this response
		//[JsonPropertyName("included")]
		//public Dictionary<string, object> Included { get; set; }

		// Appears to be empty for this response
		//[JsonPropertyName("links")]
		//public Dictionary<string, object> Links { get; set; }

		[JsonPropertyName("type")]
		public string Type { get; set; } = string.Empty;

		[JsonPropertyName("uuid")]
		public string Uuid { get; set; } = string.Empty;

		public bool Equals(Image? other)
		{
			if (ReferenceEquals(null, other))
			{
				return false;
			}

			if (ReferenceEquals(this, other))
			{
				return true;
			}

			return Attributes.Equals(other.Attributes) && Id == other.Id && Type == other.Type && Uuid == other.Uuid;
		}

		public override bool Equals(object? obj)
		{
			if (ReferenceEquals(null, obj))
			{
				return false;
			}

			if (ReferenceEquals(this, obj))
			{
				return true;
			}

			if (obj.GetType() != this.GetType())
			{
				return false;
			}

			return Equals((Image)obj);
		}

		public override int GetHashCode()
		{
			return HashCode.Combine(Attributes, Id, Type, Uuid);
		}
	}

	public class Image_Attributes : IEquatable<Image_Attributes>
	{
		[JsonPropertyName("image_type")]
		public string ImageType { get; set; } = string.Empty;

		[JsonPropertyName("large")]
		public string Large { get; set; } = string.Empty;

		[JsonPropertyName("medium")]
		public string Medium { get; set; } = string.Empty;

		[JsonPropertyName("orientation")]
		public string Orientation { get; set; } = string.Empty;

		[JsonPropertyName("small")]
		public string Small { get; set; } = string.Empty;

		[JsonPropertyName("thumb")]
		public string Thumb { get; set; } = string.Empty;

		public bool Equals(Image_Attributes? other)
		{
			if (ReferenceEquals(null, other))
			{
				return false;
			}

			if (ReferenceEquals(this, other))
			{
				return true;
			}

			return ImageType == other.ImageType && Large == other.Large && Medium == other.Medium && Orientation == other.Orientation && Small == other.Small && Thumb == other.Thumb;
		}

		public override bool Equals(object? obj)
		{
			if (ReferenceEquals(null, obj))
			{
				return false;
			}

			if (ReferenceEquals(this, obj))
			{
				return true;
			}

			if (obj.GetType() != this.GetType())
			{
				return false;
			}

			return Equals((Image_Attributes)obj);
		}

		public override int GetHashCode()
		{
			return HashCode.Combine(ImageType, Large, Medium, Orientation, Small, Thumb);
		}
	}
}
