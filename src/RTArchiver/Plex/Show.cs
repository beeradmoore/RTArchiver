using System.Diagnostics;
using System.Text;
using Serilog;

namespace RTArchiver.Plex;

/*
https://github.com/mrzhenya/plex-plugins/blob/master/localmetadata/docs/templates/show.info

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
10.0
[studio]
REPLACE ME
[originally_available_at]
2000-09-01
[collections]
REPLACE ME
REPLACE ME
[genres]
REPLACE ME
REPLACE ME
*/

public class Show
{
	public string Title { get; set; } = string.Empty;

	public string Summary { get; set; } = string.Empty;

	public string OriginallyAvailableAt { get; set; } = string.Empty;

	public List<string> Collections { get; set; } = new List<string>();
	
	public List<string> Genres { get; set; } = new List<string>();

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
		
		if (Collections.Any())
		{
			stringBuilder.AppendLine("[collections]");
			foreach (var collection in Collections)
			{
				stringBuilder.AppendLine(collection);
			}
		}
		
		if (Genres.Any())
		{
			stringBuilder.AppendLine("[genres]");
			foreach (var genre in Genres)
			{
				stringBuilder.AppendLine(genre);
			}
		}
		
		return stringBuilder.ToString();
	}

	public static Show FromRTShow(Data.Show rtShow)
	{
		var show = new Show();
		show.Title = rtShow.Title;
		show.Summary = rtShow.Attributes.Summary;
		show.Genres.AddRange(rtShow.Attributes.Genres);

		if (DateTime.TryParse(rtShow.Attributes.PublishedAt, out DateTime publishedAt) == true)
		{
			show.OriginallyAvailableAt = TimeZoneInfo.ConvertTime(publishedAt, CentralTimeZone).ToString("yyyy-MM-dd");
		}
		else
		{
			Log.Error($"PublishedAt could not be determined from {rtShow.Attributes.PublishedAt} in Plex.Show.FromRTShow");
		}
		
		return show;
	}
}