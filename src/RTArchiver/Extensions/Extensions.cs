using System.Text.RegularExpressions;
using RTArchiver.Data;

namespace RTArchiver.Extensions;

public static class Extensions
{
	static string GetCleanFileName(string fileName)
	{
		return Regex.Replace(fileName, """\\|\/|\:|\*|\?|\"|\'|\<|\>|\|""", string.Empty);
	}
	
	public static string FileName(this Episode episode)
	{
		return $"S{episode.Attributes.SeasonNumber} E{episode.Attributes.Number} {GetCleanFileName(episode.Attributes.Title)}.mkv";
	}
	
	public static string FileName(this BonusFeature bonusFeature)
	{
		return $"E{bonusFeature.Attributes.Number} {GetCleanFileName(bonusFeature.Attributes.Title)}.mkv";
	}

	public static string FullLocalPath(this Episode episode)
	{
		return Path.Combine(Storage.VideosPath, episode.Attributes.ChannelSlug, episode.Attributes.ShowSlug, "seasons", episode.Attributes.SeasonSlug, episode.FileName());
	}

	public static string FullLocalPath(this BonusFeature bonusFeature, Show show)
	{
		return Path.Combine(Storage.VideosPath, bonusFeature.Attributes.ChannelSlug, show.Slug, "bonus_features", bonusFeature.Attributes.Slug, bonusFeature.FileName());
	}
	// P
	

}