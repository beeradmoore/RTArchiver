using System.Text;
using Serilog;

namespace RTArchiver.Plex;


/*
https://github.com/mrzhenya/plex-plugins/blob/master/localmetadata/docs/templates/episode.info

###
### Template for an episode.info file that should be used for
### Show episodes parsed with 'Local Metadata Info Files' agent.
###
[title]
REPLACE ME
[summary]
REPLACE ME
[content_rating]
PG
[rating]
9.9
[originally_available_at]
2000-09-01
[directors]
REPLACE ME
REPLACE ME
[writers]
REPLACE ME
REPLACE ME
*/

public class Episode
{
	public string Title { get; set; } = string.Empty;

	public string Summary { get; set; } = string.Empty;

	public string OriginallyAvailableAt { get; set; } = string.Empty;
	
	static readonly TimeZoneInfo CentralTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");

	public override string ToString()
	{
		var stringBuilder = new StringBuilder();

		if (string.IsNullOrEmpty(Title) == false)
		{
			stringBuilder.AppendLine($"[title]\n{Title}");
		}
		
		if (string.IsNullOrEmpty(Summary) == false)
		{
			stringBuilder.AppendLine($"[summary]\n{Summary}");
		}
		
		if (string.IsNullOrEmpty(OriginallyAvailableAt) == false)
		{
			stringBuilder.AppendLine($"[originally_available_at]\n{OriginallyAvailableAt}");
		}
		
		return stringBuilder.ToString();
	}
	
	public static Episode FromRTEpisode(Data.Episode rtEpisode)
	{
		var episode = new Episode();
		episode.Title = rtEpisode.Attributes.Title;
		episode.Summary = rtEpisode.Attributes.Description;
		
		if (DateTime.TryParse(rtEpisode.Attributes.PublicGoLiveAt, out DateTime publishedAt) == true)
		{
			episode.OriginallyAvailableAt = TimeZoneInfo.ConvertTime(publishedAt, CentralTimeZone).ToString("yyyy-MM-dd");
		}
		else
		{
			Log.Error($"PublishedAt could not be determined from {rtEpisode.Attributes.PublicGoLiveAt} in Plex.Episode.FromRTEpisode");
		}
		
		return episode;
	}
	
	public static Episode FromRTBonusFeature(Data.BonusFeature rtBonusFeature)
	{
		var episode = new Episode();
		episode.Title = rtBonusFeature.Attributes.Title;
		episode.Summary = rtBonusFeature.Attributes.Description;
		
		if (DateTime.TryParse(rtBonusFeature.Attributes.PublicGoLiveAt, out DateTime publishedAt) == true)
		{
			episode.OriginallyAvailableAt = TimeZoneInfo.ConvertTime(publishedAt, CentralTimeZone).ToString("yyyy-MM-dd");
		}
		else
		{
			Log.Error($"PublishedAt could not be determined from {rtBonusFeature.Attributes.PublicGoLiveAt} in Plex.Episode.FromRTBonusFeature");
		}
		
		return episode;
	}
}