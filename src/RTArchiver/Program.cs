using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using RTArchiver;
using RTArchiver.Data;
using RunProcessAsTask;

Console.WriteLine("~~ Rooster Teeth Archiver ~~");

var hasLaunchWarnings = false;

// Check ffmpeg exists
try
{
	var processResults = await ProcessEx.RunAsync("ffmpeg", "-version");
	if (processResults.ExitCode != 0)
	{
		throw new Exception("Could not find ffmpeg in system path.");
	}
}
catch (Exception err)
{
	Console.WriteLine("Warning: ffmpeg");
	Console.WriteLine(err.Message);
	Console.WriteLine("Make sure ffmpeg is installed.");
	Console.WriteLine("https://ffmpeg.org//download.html");
	if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
	{
		Console.WriteLine("> winget install -e --id Gyan.FFmpeg");
	}
	hasLaunchWarnings = true;
}

// Check yt-dlp exists
try
{
	var processResults = await ProcessEx.RunAsync("yt-dlp", "--version");
	if (processResults.ExitCode != 0)
	{
		throw new Exception("Could not find yt-dlp in system path.");
	}
}
catch (Exception err)
{
	Console.WriteLine("Warning: yt-dlp");
	Console.WriteLine(err.Message);
	Console.WriteLine("Make sure yt-dlp is installed.");
	Console.WriteLine("- https://github.com/yt-dlp/yt-dlp");
	if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
	{
		Console.WriteLine("> winget install -e --id yt-dlp.yt-dlp");
	}
	hasLaunchWarnings = true;
}


if (hasLaunchWarnings)
{
	// Wait 5 seconds if there is launch warnings.
	await Task.Delay(5000);
}

var rtClient = new RTClient();

// RTClient loads auth.json, so we may already be logged in.
if (rtClient.IsLoggedIn() == false)
{
	Console.WriteLine("Please to your roosterteeth.com account");

	Console.Write("Username: ");
	var username = Console.ReadLine() ?? string.Empty;

	// Get the password but hide the text.
	Console.Write("Password: ");
	var password = string.Empty;
	while (true)
	{
		var consoleKeyInfo = Console.ReadKey(true);
		if (consoleKeyInfo.Key == ConsoleKey.Enter)
		{
			break;
		}

		if (consoleKeyInfo.Key == ConsoleKey.Backspace)
		{
			if (password.Length > 0)
			{
				password = password.Remove(password.Length - 1);
			}
		}
		else if (char.IsControl(consoleKeyInfo.KeyChar) == false)
		{
			password += consoleKeyInfo.KeyChar;
		}

		// Draw password placeholder.
		Console.SetCursorPosition(0, Console.CursorTop);
		Console.Write(new string(' ', Console.BufferWidth));
		Console.SetCursorPosition(0, Console.CursorTop);
		Console.Write("Password: ");
		Console.Write(string.Empty.PadLeft(password.Length, '*'));
	}

	// Login with the new details.
	var didLogin = await rtClient.Login(username, password);
	if (didLogin == false)
	{
		return;
	}
}

var meResponse = await rtClient.GetMe();
Console.WriteLine($"Welcome {meResponse?.Attributes.Username}");

Console.WriteLine("\nLoading genres");
var genres = await rtClient.GetGenres();
Console.WriteLine($"Found: {genres.Count}");
/*
if (genres.Count > 0)
{
	Console.WriteLine(JsonSerializer.Serialize(genres[0], new JsonSerializerOptions { WriteIndented = true }));
}
*/

Console.WriteLine("\nLoading channels");
var channels = await rtClient.GetChannels();
channels.Sort((a, b) => a.Name.CompareTo(b.Name));
Console.WriteLine($"Found: {channels.Count}");

/*
if (channels.Count > 0)
{
	Console.WriteLine(JsonSerializer.Serialize(channels[0], new JsonSerializerOptions { WriteIndented = true }));
}
*/

Console.WriteLine("\nLoading shows");
var shows = await rtClient.GetShows();
Console.WriteLine($"Found: {shows.Count}");
/*
if (shows.Count > 0)
{
	Console.WriteLine(JsonSerializer.Serialize(shows[0], new JsonSerializerOptions { WriteIndented = true }));
}
*/


Channel? SelectChannel()
{
	do
	{
		Console.WriteLine("\n\n");
		
		for (var i = 0; i < channels.Count; ++i)
		{
			Console.WriteLine($"{i+1}. {channels[i].Name}");
		}

		Console.WriteLine(" q. Quit");
		
		Console.Write("Select channel: ");
		var channelSelected = Console.ReadLine()?.Trim() ?? string.Empty;

		if (string.Equals(channelSelected, "q", StringComparison.OrdinalIgnoreCase))
		{
			Environment.Exit(0);
		}

		if (int.TryParse(channelSelected, out int channelNumber) == true)
		{
			channelNumber -= 1;
			if (channelNumber >= 0 && channelNumber < channels.Count)
			{
				return channels[channelNumber];
			}
		}
	} while (true);
}

Show? SelectShow(Channel channel)
{
	var channelShows = shows.Where(x => x.Attributes.ChannelSlug == channel.Slug).ToList();
	channelShows.Sort((a, b) => a.Title.CompareTo(b.Title));
	do
	{
		Console.WriteLine("\n\n");
		
		for (var i = 0; i < channelShows.Count; ++i)
		{
			Console.WriteLine($"{i+1}. {channelShows[i].Title}");
		}

		Console.WriteLine(" b. Back");
		Console.WriteLine(" q. Quit");
		
		Console.Write("Select show: ");
		var showSelected = Console.ReadLine()?.Trim() ?? string.Empty;

		if (string.Equals(showSelected, "q", StringComparison.OrdinalIgnoreCase))
		{
			Environment.Exit(0);
		}
		else if (string.Equals(showSelected, "b", StringComparison.OrdinalIgnoreCase))
		{
			return null;
		}
		else if (int.TryParse(showSelected, out int showNumber) == true)
		{
			showNumber -= 1;
			if (showNumber >= 0 && showNumber < channelShows.Count)
			{
				return channelShows[showNumber];
			}
		}
	} while (true);
}


DownloadOptions? SelectDownloadOption(Channel channel, Show show)
{
	do
	{
		Console.WriteLine("\n\n");
		Console.WriteLine($"{channel.Name} - {show.Title}");
		Console.WriteLine("\n\n");
		Console.WriteLine("1. Download everything");
		Console.WriteLine("2. Download all seasons");
		Console.WriteLine("3. Download specific season");
		Console.WriteLine("4. Download all behind the scenes");
		Console.WriteLine("5. Download specific behind the scenes");
		Console.WriteLine("b. Back");
		Console.WriteLine("q. Quit");
		
		Console.Write("Download option: ");
		var downloadOptionSelected = Console.ReadLine()?.Trim() ?? string.Empty;

		if (string.Equals(downloadOptionSelected, "q", StringComparison.OrdinalIgnoreCase))
		{
			Environment.Exit(0);
		}
		else if (string.Equals(downloadOptionSelected, "b", StringComparison.OrdinalIgnoreCase))
		{
			return null;
		}
		else if (int.TryParse(downloadOptionSelected, out int downloadOption) == true)
		{
			if (downloadOption >= 1 && downloadOption <= 5)
			{
				return downloadOption switch
				{
					1 => RTArchiver.Data.DownloadOptions.Everything,
					2 => RTArchiver.Data.DownloadOptions.AllSeasons,
					3 => RTArchiver.Data.DownloadOptions.SpecificSeason,
					4 => RTArchiver.Data.DownloadOptions.AllBonusFeatures,
					5 => RTArchiver.Data.DownloadOptions.SpecificBonusFeature,
					_ => null, // this should never happen.
				};
			}
		}
	} while (true);
}



async Task<Season?> SelectSeason(Show show)
{
	var seasons = await rtClient.GetSeasons(show.Slug);
	seasons.Sort((a, b) => a.Attributes.Number.CompareTo(b.Attributes.Number));
	
	do
	{
		Console.WriteLine("\n\n");
		
		for (var i = 0; i < seasons.Count; ++i)
		{
			Console.WriteLine($"{i+1}. {seasons[i].Attributes.Title}");
		}

		Console.WriteLine("b. Back");
		Console.WriteLine("q. Quit");
		
		Console.Write("Select season: ");
		var seasonSelected = Console.ReadLine()?.Trim() ?? string.Empty;

		if (string.Equals(seasonSelected, "q", StringComparison.OrdinalIgnoreCase))
		{
			Environment.Exit(0);
		}
		else if (string.Equals(seasonSelected, "b", StringComparison.OrdinalIgnoreCase))
		{
			return null;
		}
		else if (int.TryParse(seasonSelected, out int seasonNumber) == true)
		{
			seasonNumber -= 1;
			if (seasonNumber >= 0 && seasonNumber < seasons.Count)
			{
				return seasons[seasonNumber];
			}
		}
	} while (true);
}



async Task<BonusFeature?> SelectBonusFeature(Show show)
{
	var bonusFeatures = await rtClient.GetBonusFeatures(show.Slug);
	bonusFeatures.Sort((a, b) => a.Attributes.Number.CompareTo(b.Attributes.Number));
	
	do
	{
		Console.WriteLine("\n\n");
		
		for (var i = 0; i < bonusFeatures.Count; ++i)
		{
			Console.WriteLine($"{i+1}. {bonusFeatures[i].Attributes.Title}");
		}

		Console.WriteLine(" b. Back");
		Console.WriteLine(" q. Quit");
		
		Console.Write("Select bonus feature: ");
		var selectedBonusFeature = Console.ReadLine()?.Trim() ?? string.Empty;

		if (string.Equals(selectedBonusFeature, "q", StringComparison.OrdinalIgnoreCase))
		{
			Environment.Exit(0);
		}
		else if (string.Equals(selectedBonusFeature, "b", StringComparison.OrdinalIgnoreCase))
		{
			return null;
		}
		else if (int.TryParse(selectedBonusFeature, out int bonusFeatureNumber) == true)
		{
			bonusFeatureNumber -= 1;
			if (bonusFeatureNumber >= 0 && bonusFeatureNumber < bonusFeatures.Count)
			{
				return bonusFeatures[bonusFeatureNumber];
			}
		}
	} while (true);
}

while (true)
{
	var selectedChannel = SelectChannel();
	if (selectedChannel == null)
	{
		continue;
	}

	var selectedShow = SelectShow(selectedChannel);
	if (selectedShow == null)
	{
		continue;
	}
	
	var selectedDownloadOption = SelectDownloadOption(selectedChannel, selectedShow);
	if (selectedDownloadOption == null)
	{
		// Technically this takes you back to the first option.
		continue;
	}

	if (selectedDownloadOption == DownloadOptions.Everything)
	{	
		await rtClient.DownloadEverything(selectedChannel, selectedShow);
	}
	else if (selectedDownloadOption == DownloadOptions.AllSeasons)
	{
		await rtClient.DownloadAllSeasons(selectedChannel, selectedShow);
	}
	else if (selectedDownloadOption == DownloadOptions.SpecificSeason)
	{
		var selectedSeason = await SelectSeason(selectedShow);
		if (selectedSeason == null)
		{
			// This isn't really back, this is start again.
			continue;
		}
		await rtClient.DownloadSpecificSeason(selectedChannel, selectedShow, selectedSeason);
	}
	else if (selectedDownloadOption == DownloadOptions.AllBonusFeatures)
	{	
		await rtClient.DownloadAllBonusFeatures(selectedChannel, selectedShow);
	}
	else if (selectedDownloadOption == DownloadOptions.SpecificBonusFeature)
	{
		var selectedBonusFeature = await SelectBonusFeature(selectedShow);
		if (selectedBonusFeature == null)
		{
			// This isn't really back, this is start again.
			continue;
		}
		await rtClient.DownloadSpecificBonusFeature(selectedChannel, selectedShow, selectedBonusFeature);
	}
	

	
	Debugger.Break();
}

/*
if (genres.Count > 0)
{
	Console.WriteLine(JsonSerializer.Serialize(genres[0], new JsonSerializerOptions { WriteIndented = true }));
}
var slug = "camp-camp";
Console.WriteLine($"\nLoading seasons for {slug}");
var seasons = await rtClient.GetSeasons(slug);
Console.WriteLine($"Found: {seasons.Count}");
if (seasons.Count > 0)
{
	Console.WriteLine(JsonSerializer.Serialize(seasons[0], new JsonSerializerOptions { WriteIndented = true}));
}
*/

/*
start of with a channel slug
	list shows of channel slug
		each show has a link for episodes
		each bonus-features
			 	
			 	
			 	 "seasons": "/api/v1/shows/camp-camp/seasons?order=asc\u0026order_by=number",
                  "bonus_features": "/api/v1/shows/camp-camp/bonus_features",


Console.WriteLine("\nLoading shows");
var shows = await rtClient.GetShows();
Console.WriteLine($"Found: {shows.Count}");
if(shows.Count > 0)
{
	Console.WriteLine(JsonSerializer.Serialize(shows[0], new JsonSerializerOptions { WriteIndented = true }));
}
*/
