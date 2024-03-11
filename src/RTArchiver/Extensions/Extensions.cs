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
		return $"S{episode.Attributes.SeasonNumber} E{episode.Attributes.Number} {GetCleanFileName(episode.Attributes.Title)}.mp4";
	}
	
	public static string FileName(this BonusFeature bonusFeature)
	{
		return $"E{bonusFeature.Attributes.Number} {GetCleanFileName(bonusFeature.Attributes.Title)}.mp4";
	}
	

}