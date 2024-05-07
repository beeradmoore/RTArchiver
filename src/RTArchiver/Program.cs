using System.Collections.Frozen;
using System.CommandLine;
using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;
using RTArchiver;
using RTArchiver.Data;
using RTArchiver.Data.Responses;
using RunProcessAsTask;
using Serilog;


class Program
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
	static RTClient _rtClient;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
	
	static async Task<int> Main(string[] args)
	{
		var rootCommand = new RootCommand("Archives content from roosterteeth.com.");
		
		var globalOutputOption = new Option<string>(new string[] { "--output", "-o" }, () =>
		{
			var envRTArchivePath = Environment.GetEnvironmentVariable("RT_ARCHIVE_PATH") ?? string.Empty;
			if (String.IsNullOrEmpty(envRTArchivePath) == false)
			{
				return envRTArchivePath;
			}
			return Path.Combine(Directory.GetCurrentDirectory(), "output");
		}, "The archive output folder path. You can also set the environment variable RT_ARCHIVE_PATH and omit output path to accomplish the same thing.");
		rootCommand.AddGlobalOption(globalOutputOption);
		
		var globalThreadsOption = new Option<int>(new string[] { "--threads", "-t" }, () => Environment.ProcessorCount , "Sets the number of threads to use to execute multiple things at once (eg. multiple web requests, multiple downloads)");
		rootCommand.AddGlobalOption(globalThreadsOption);
		
		var globalUseCacheOption = new Option<bool>(new string[] { "--use-cache" }, () => true, "Use the cache on the local disk if found. You must set this to false to refresh the cache");
		rootCommand.AddGlobalOption(globalUseCacheOption); 
		
		
		var downloadCommand = new Command("download", "Downloads data from roosterteeth.com.");
		var downloadApiOption = new Option<bool>(new string[] { "--api" }, () => false, "Downloads all api data (est. 8gb).");
		var downloadChannelOption = new Option<string>(new string[] { "--channel" }, "Downloads everything on a specific channels stub");
		var downloadShowOption = new Option<string>(new string[] { "--show" }, "Downloads everything on a specific show stub.");
		//var downloadSitemapOption = new Option<bool>(new string[] { "--sitemap" }, () => false, "Downloads sitemap.xml (est. 40mb)");
		var downloadConcurrentFragmentsOption = new Option<int>(new string[] { "--concurrent-fragments", "-cf" }, () => 10, "Sets the number of concurrent fragments used in yt-dlp.");
		downloadCommand.AddOption(downloadApiOption);
		downloadCommand.AddOption(downloadChannelOption);
		downloadCommand.AddOption(downloadShowOption);
		//downloadCommand.AddOption(downloadSitemapOption);
		downloadCommand.AddOption(downloadConcurrentFragmentsOption);
		//downloadCommand.SetHandler(DownloadAsync, globalOutputOption, globalThreadsOption, globalUseCacheOption, downloadApiOption, downloadChannelOption, downloadShowOption, downloadSitemapOption, downloadConcurrentFragmentsOption);

		//var downloadBuildCommand = new Command("build", "Builds a download manifest which can be processed by \"download process\"");
		//downloadBuildCommand.AddOption(downloadChannelOption);
		//downloadBuildCommand.AddOption(downloadShowOption);
		//ownloadBuildCommand.AddOption(downloadSitemapOption);
		//downloadBuildCommand.AddOption(downloadConcurrentFragmentsOption);
		//downloadBuildCommand.SetHandler(DownloadBuildAsync, globalOutputOption, globalThreadsOption, globalUseCacheOption, downloadChannelOption, downloadShowOption, downloadConcurrentFragmentsOption);
		//downloadCommand.AddCommand(downloadBuildCommand);

		var downloadImagesCommand = new Command("images", "Downloads all images for current api cache.");
		downloadImagesCommand.SetHandler(DownloadImagesAsync, globalOutputOption, globalThreadsOption, globalUseCacheOption);
		downloadCommand.AddCommand(downloadImagesCommand);
		
		var downloadVideosCommand = new Command("videos", "Downloads all videos for current api cache.");
		downloadVideosCommand.SetHandler(DownloadVideosAsync, globalOutputOption, globalThreadsOption, globalUseCacheOption);
		downloadCommand.AddCommand(downloadVideosCommand);
		
		var downloadSitemapsCommand = new Command("sitemaps", "Downloads all videos for current api cache.");
		downloadSitemapsCommand.SetHandler(DownloadSitemapsAsync, globalOutputOption, globalThreadsOption, globalUseCacheOption);
		downloadCommand.AddCommand(downloadSitemapsCommand);
		
		rootCommand.Add(downloadCommand);
		
		
		var listCommand = new Command("list", "Lists data from roosterteeth.com, used to find stubs for the download command.");
		
		var listChannelsCommand = new Command("channels", "Lists all channels.");
		listChannelsCommand.SetHandler(ListChannelsAsync, globalOutputOption, globalThreadsOption, globalUseCacheOption);
		listCommand.AddCommand(listChannelsCommand);
		
		var listGenresommand = new Command("genres", "Lists all genres.");
		listGenresommand.SetHandler(ListGenresAsync, globalOutputOption, globalThreadsOption, globalUseCacheOption);
		listCommand.AddCommand(listGenresommand);
		
		var listShowsCommand = new Command("shows", "Lists all shows.");
		var listShowsChannelOption = new Option<string>(new string[] { "--channel" }, "List shows for specific channel slug. Leave empty to fetch all shows.");
		listShowsCommand.AddOption(listShowsChannelOption);
		listShowsCommand.SetHandler(ListShowsAsync, globalOutputOption, globalThreadsOption, globalUseCacheOption, listShowsChannelOption);
		listCommand.AddCommand(listShowsCommand);
		rootCommand.Add(listCommand);
		
		var crawlCommand = new Command("crawl", "Crawls over every endpoint it can find, attempts to 100% fill the API cache.");
		crawlCommand.SetHandler(CrawlAsync, globalOutputOption, globalThreadsOption, globalUseCacheOption);
		rootCommand.Add(crawlCommand);
		
		
		var playgroundCommand = new Command("playground", "Easy entry point to test different code");
		playgroundCommand.SetHandler(PlaygroundAsync, globalOutputOption, globalThreadsOption, globalUseCacheOption);
		rootCommand.Add(playgroundCommand);
		
		
		
		//rootCommand.SetHandler(RunAsync, outputOption);
		return await rootCommand.InvokeAsync(args);
	}

	static async Task<bool> CheckPrerequisites()
	{
		
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
			Log.Warning("Warning: ffmpeg");
			Log.Warning(err.Message);
			Log.Warning("Make sure ffmpeg is installed.");
			Log.Warning("https://ffmpeg.org//download.html");
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				Log.Warning("> winget install -e --id Gyan.FFmpeg");
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
			Log.Warning("Warning: yt-dlp");
			Log.Warning(err.Message);
			Log.Warning("Make sure yt-dlp is installed.");
			Log.Warning("- https://github.com/yt-dlp/yt-dlp");
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				Log.Warning("> winget install -e --id yt-dlp.yt-dlp");
			}
			hasLaunchWarnings = true;
		}

		return hasLaunchWarnings;
	}

	static async Task<int> SetupClientAsync(string globalOutputPath, int globalThreads, bool globalUseCache)
	{
		Log.Information("~~ Rooster Teeth Archiver ~~");
		Storage.Init(globalOutputPath);
		
		_rtClient = new RTClient()
		{
			NumberOfThreads = globalThreads,
			UseCache = globalUseCache,
		};
		
		var didAuthenticate = await Authenticate();
		if (didAuthenticate == false)
		{
			return 1;
		}

		return 0;
	}

	/*
	static async Task<int> DownloadAsync(string globalOutputPath, int globalThreads, bool globalUseCache, bool downloadApi, string downloadChannel, string downloadShow, bool downloadSitemap, int concurrentFragments)
	{
		var setupClientResult = await SetupClientAsync(globalOutputPath, globalThreads, globalUseCache);
		if (setupClientResult != 0)
		{
			return setupClientResult;
		}
		
		Console.WriteLine($"globalOutputPath: {globalOutputPath}");
		Console.WriteLine($"globalThreads: {globalThreads}");
		Console.WriteLine($"globalUseCache: {globalUseCache}");
		Console.WriteLine($"downloadApi: {downloadApi}");
		Console.WriteLine($"downloadChannel: {downloadChannel}");
		Console.WriteLine($"downloadShow: {downloadShow}");
		Console.WriteLine($"downloadSitemap: {downloadSitemap}");
		Console.WriteLine($"concurrentFragments: {concurrentFragments}");
		
		if (await CheckPrerequisites())
		{
			// Wait 5 seconds if there is launch warnings.
			await Task.Delay(5000);
		}


		if (downloadSitemap)
		{
			await _rtClient.DownloadSitemapsAsync();
		}

		return 0;
	}
	*/

	/*
	static async Task<int> DownloadBuildAsync(string globalOutputPath, int globalThreads, bool globalUseCache, string downloadChannel, string downloadShow, int concurrentFragments)
	{
		var setupClientResult = await SetupClientAsync(globalOutputPath, globalThreads, globalUseCache);
		if (setupClientResult != 0)
		{
			return setupClientResult;
		}

		Console.WriteLine($"globalOutputPath: {globalOutputPath}");
		Console.WriteLine($"globalThreads: {globalThreads}");
		Console.WriteLine($"globalUseCache: {globalUseCache}");
		Console.WriteLine($"downloadChannel: {downloadChannel}");
		Console.WriteLine($"downloadShow: {downloadShow}");
		Console.WriteLine($"downloadSitemap: {downloadSitemap}");
		Console.WriteLine($"concurrentFragments: {concurrentFragments}");

		if (await CheckPrerequisites())
		{
			// Wait 5 seconds if there is launch warnings.
			await Task.Delay(5000);
		}


		_rtClient = new RTClient() { NumberOfThreads = globalThreads, UseCache = true, };

		var didAuthenticate = await Authenticate();
		if (didAuthenticate == false)
		{
			return 1;
		}

		var channels = await _rtClient.GetChannels();
		var shows = await _rtClient.GetShows();
		
		foreach (var channel in channels)
		{
			foreach (var show in shows)
			{
				if (channel.Slug == show.Attributes.ChannelSlug)
				{
					Log.Information($"Name: {show.Title}");
					Log.Information($"Show slug: {show.Slug}\n");
					Log.Information($"Channel slug: {show.Attributes.ChannelSlug}");
				}
			}
			
			Debugger.Break();

		}

		
		Debugger.Break();

		return 1;
	}
	*/
	
	static async Task<int> DownloadImagesAsync(string globalOutputPath, int globalThreads, bool globalUseCache)
	{
		var setupClientResult = await SetupClientAsync(globalOutputPath, globalThreads, globalUseCache);
		if (setupClientResult != 0)
		{
			return setupClientResult;
		}

		await _rtClient.DownloadImagesAsync();

		return 0;
	}
	
	
	static async Task<int> DownloadVideosAsync(string globalOutputPath, int globalThreads, bool globalUseCache)
	{
		var setupClientResult = await SetupClientAsync(globalOutputPath, globalThreads, globalUseCache);
		if (setupClientResult != 0)
		{
			return setupClientResult;
		}

		await _rtClient.DownloadVideosAsync();

		return 0;
	}
	
	static async Task<int> DownloadSitemapsAsync(string globalOutputPath, int globalThreads, bool globalUseCache)
	{
		var setupClientResult = await SetupClientAsync(globalOutputPath, globalThreads, globalUseCache);
		if (setupClientResult != 0)
		{
			return setupClientResult;
		}

		await _rtClient.DownloadSitemapsAsync();

		return 0;
	}
	
	static async Task<int> PlaygroundAsync(string globalOutputPath, int globalThreads, bool globalUseCache)
	{
		var setupClientResult = await SetupClientAsync(globalOutputPath, globalThreads, globalUseCache);
		if (setupClientResult != 0)
		{
			return setupClientResult;
		}

		await _rtClient.PlaygroundAsync();


		return 0;
	}
	
	

	static async Task<bool> Authenticate()
	{
		// RTClient loads auth.json, so we may already be logged in.
		if (_rtClient.IsLoggedIn() == false)
		{
			Log.Information("Please login to your roosterteeth.com account. Leave inputs blank to not login.");

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

			if (string.IsNullOrEmpty(username) && string.IsNullOrEmpty(password))
			{
				return true;
			}

			// Login with the new details.
			var didLogin = await _rtClient.Login(username, password);
			if (didLogin == false)
			{
				return false;
			}
		}

		var meResponse = await _rtClient.GetMe();
		if (meResponse is null)
		{
			Log.Error("Could not fetch profile data. Check the error log for more information.");
			return false;
		}
		
		Log.Information($"Welcome {meResponse?.Attributes.Username}");
		return true;
	}

	static async Task<int> ListChannelsAsync(string globalOutputPath, int globalThreads, bool globalUseCache)
	{
		var setupClientResult = await SetupClientAsync(globalOutputPath, globalThreads, globalUseCache);
		if (setupClientResult != 0)
		{
			return setupClientResult;
		}
		
		Log.Information("Listing channels");
		var channels = await _rtClient.GetChannels();
		Log.Information($"Found {channels.Count} channels.");
		foreach (var channel in channels)
		{
			Log.Information($"Name: {channel.Name}");
			Log.Information($"Channel slug: {channel.Slug}\n");
		}
		
		return 1;
	}

	static async Task<int> ListGenresAsync(string globalOutputPath, int globalThreads, bool globalUseCache)
	{
		var setupClientResult = await SetupClientAsync(globalOutputPath, globalThreads, globalUseCache);
		if (setupClientResult != 0)
		{
			return setupClientResult;
		}
		
		Log.Information("Listing genres");
		var genres = await _rtClient.GetGenres();
		Log.Information($"Found {genres.Count} genres.");
		foreach (var genre in genres)
		{
			Log.Information($"Name: {genre.Name}");
			Log.Information($"Genre slug: {genre.Slug}\n");
		}
		
		return 1;
	}
	
	static async Task<int> ListShowsAsync(string globalOutputPath, int globalThreads, bool globalUseCache, string channel = "")
	{
		var setupClientResult = await SetupClientAsync(globalOutputPath, globalThreads, globalUseCache);
		if (setupClientResult != 0)
		{
			return setupClientResult;
		}

		var filterChannels = (string.IsNullOrEmpty(channel) == false);
		
		Log.Information("Listing shows");
		var shows = await _rtClient.GetShows();
		Log.Information($"Found {shows.Count} shows.");
		foreach (var show in shows)
		{
			if (filterChannels && channel == show.Attributes.ChannelSlug)
			{
				Log.Information($"Name: {show.Title}");
				Log.Information($"Show slug: {show.Slug}\n");
				Log.Information($"Channel slug: {show.Attributes.ChannelSlug}");
			}
		}

		return 1;
	}

	static async Task<int> CrawlAsync(string globalOutputPath, int globalThreads, bool globalUseCache)
	{
		var setupClientResult = await SetupClientAsync(globalOutputPath, globalThreads, false);
		if (setupClientResult != 0)
		{
			return setupClientResult;
		}
		
		var rtClientApiCrawler = new RTClientAPICrawler(_rtClient);
		rtClientApiCrawler.StartAndWait();
		
		Log.Information("Crawl finished.");
		
		return 1;
	}
	
	
}



/*
var jsonFiles = Directory.GetFiles(Path.Combine(Storage.CachePath, "api", "v1", "watch"), "*.json", SearchOption.AllDirectories);

List<string> episodes = new List<string>();
List<string> bonusFeatures = new List<string>();
List<string> videos = new List<string>();
Dictionary<long, int> ids = new Dictionary<long, int>(jsonFiles.Length);

var parallelOptions2 = new ParallelOptions()
{
	MaxDegreeOfParallelism = 24,
};
await Parallel.ForEachAsync(jsonFiles, parallelOptions2, async (jsonFile, state) =>
{
	//var fileData = File.ReadAllText(jsonFile);
	using (var fileStream = File.OpenRead(jsonFile))
	{
		var videosResponse = await JsonSerializer.DeserializeAsync<RTArchiver.Data.Responses.VideosResponse>(fileStream);

		if (videosResponse is null)
		{
			Debugger.Break();
			return;
		}

		if (videosResponse.Data.Count == 0)
		{
			
			Console.WriteLine($"Zero - {jsonFile}");
			return;
		}
		

		if (videosResponse.Data.Count > 1)
		{
			Debugger.Break();
			return;
		}

		if (videosResponse.Data[0].Type == "episode")
		{
			episodes.Add(jsonFile);
		}
		else if (videosResponse.Data[0].Type == "bonus_feature")
		{
			bonusFeatures.Add(jsonFile);
		}
		else if (videosResponse.Data[0].Type == "video")
		{
			videos.Add(jsonFile);
		}
		else
		{
			Debugger.Break();
		}

		var id = videosResponse.Data[0].Id;
		try
		{
			if (ids.TryAdd(id, 1) == false)
			{
				++ids[id];
			}
		}
		catch (Exception err)
		{
			Console.WriteLine(err);
			Debugger.Break();
		}
		
	}
});


var frozenDictionary = ids.ToFrozenDictionary();
var idKeys = frozenDictionary.Keys.ToList();
idKeys.Sort();

using (var fileStream = File.Create("episode_ids.txt"))
{
	using (var streamWriter = new StreamWriter(fileStream))
	{
		foreach (var id in idKeys)
		{
			try
			{
				streamWriter.WriteLine($"{id} - {frozenDictionary[id]}");
			}
			catch (Exception err)
			{
				Console.WriteLine($"{id} not found in frozenDictionary");
			}

		}
	}
}

void SaveList(string filename, List<string> list)
{
	using (var fileStream = File.Create(filename))
	{
		using (var streamWriter = new StreamWriter(fileStream))
		{
			foreach (var item in list)
			{
				streamWriter.WriteLine(item);
			}
		}
	}
}

SaveList("episodes.txt", episodes);
SaveList("bonus_features.txt", bonusFeatures);
SaveList("videos.txt", videos);

Debugger.Break();

return;
*/

/*
var jsonFiles = Directory.GetFiles(Storage.CachePath, "*.json", SearchOption.AllDirectories);
var regexId = new Regex("\"id\":(?<id>((\\d*)|(\"(.*)\")))");
var regexUuid = new Regex("\"uuid\":\"(?<uuid>(([a-zA-Z0-9]{8})-([a-zA-Z0-9]{4})-([a-zA-Z0-9]{4})-([a-zA-Z0-9]{4})-([a-zA-Z0-9]{12})))\"");


Dictionary<long, int> ids = new Dictionary<long, int>();
Dictionary<string, int> uuids = new Dictionary<string, int>();

foreach (var jsonFile in jsonFiles)
{
	var data = File.ReadAllText(jsonFile);
	
	var idMatches = regexId.Matches(data);
	var uuidMatches = regexUuid.Matches(data);

	var idTask = Task.Run(() =>
	{
		if (idMatches.Any())
		{
			foreach (Match idMatch in idMatches)
			{
				if (long.TryParse(idMatch.Groups["id"].ValueSpan, out long id) == true)
				{
					if (ids.TryAdd(id, 1) == false)
					{
						++ids[id];
					}
				}
				else
				{
					Console.WriteLine($"\"{idMatch.Groups["id"].Value}\" is not a valid id.");
					//Debugger.Break();
				}
			}
		}
	});
	
	
	var uuidTask = Task.Run(() =>
	{
		if (uuidMatches.Any())
		{
			foreach (Match uuidMatch in uuidMatches)
			{
				var value = uuidMatch.Groups["uuid"].Value;
				if (uuids.TryAdd(value, 1) == false)
				{
					++uuids[value];
				}
			}
		}
	});

	await Task.WhenAll(idTask, uuidTask);


	//break;
	//Debugger.Break();


}

var idKeys = ids.Keys.ToList();
idKeys.Sort();

using (var fileStream = File.Create("ids.txt"))
{
	using (var streamWriter = new StreamWriter(fileStream))
	{
		foreach (var id in idKeys)
		{
			streamWriter.WriteLine($"{id} - {ids[id]}");
		}
	}
}

idKeys.Clear();

var uuidKeys = uuids.Keys.ToList();
uuidKeys.Sort();

using (var fileStream = File.Create("uuids.txt"))
{
	using (var streamWriter = new StreamWriter(fileStream))
	{
		foreach (var uuid in uuidKeys)
		{
			streamWriter.WriteLine($"{uuid} - {uuids[uuid]}");
		}
	}
}

uuidKeys.Clear();

Debugger.Break();

*/





/*


await rtClient.DownloadAllAsync();
return;

//await rtClient.GetGenres();
//await rtClient.GetChannels();
await rtClient.CacheGoBrrrr();

Console.WriteLine("Done.");
return;
Console.WriteLine("\nLoading genres");
var genres = await rtClient.GetGenres();
Console.WriteLine($"Found: {genres.Count}");
*/

/*
if (genres.Count > 0)
{
	Console.WriteLine(JsonSerializer.Serialize(genres[0], new JsonSerializerOptions { WriteIndented = true }));
}
*/

/*
Console.WriteLine("\nLoading channels");
var channels = await rtClient.GetChannels();
channels.Sort((a, b) => a.Name.CompareTo(b.Name));
Console.WriteLine($"Found: {channels.Count}");
*/

/*
if (channels.Count > 0)
{
	Console.WriteLine(JsonSerializer.Serialize(channels[0], new JsonSerializerOptions { WriteIndented = true }));
}
*/

/*
Console.WriteLine("\nLoading shows");
var shows = await rtClient.GetShows();
Console.WriteLine($"Found: {shows.Count}");
*/
/*
if (shows.Count > 0)
{
	Console.WriteLine(JsonSerializer.Serialize(shows[0], new JsonSerializerOptions { WriteIndented = true }));
}
*/


/*
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

	var downloadItems = new List<DownloadItem>();

	if (selectedDownloadOption == DownloadOptions.Everything)
	{
		var newDownloadItems = await rtClient.GetDownloadItemsForEverything(selectedChannel, selectedShow);
		downloadItems.AddRange(newDownloadItems);
	}
	else if (selectedDownloadOption == DownloadOptions.AllSeasons)
	{
		var newDownloadItems = await rtClient.GetDownloadItemsForAllSeasons(selectedChannel, selectedShow);
		downloadItems.AddRange(newDownloadItems);
	}
	else if (selectedDownloadOption == DownloadOptions.SpecificSeason)
	{
		var selectedSeason = await SelectSeason(selectedShow);
		if (selectedSeason == null)
		{
			// This isn't really back, this is start again.
			continue;
		}
		var newDownloadItems = await rtClient.GetDownloadItemsForSpecificSeason(selectedChannel, selectedShow, selectedSeason);
		downloadItems.AddRange(newDownloadItems);
	}
	else if (selectedDownloadOption == DownloadOptions.AllBonusFeatures)
	{	
		var newDownloadItems = await rtClient.GetDownloadItemsForAllBonusFeatures(selectedChannel, selectedShow);
		downloadItems.AddRange(newDownloadItems);
	}
	else if (selectedDownloadOption == DownloadOptions.SpecificBonusFeature)
	{
		var selectedBonusFeature = await SelectBonusFeature(selectedShow);
		if (selectedBonusFeature == null)
		{
			// This isn't really back, this is start again.
			continue;
		}
		var newDownloadItems = await rtClient.GetDownloadItemsForSpecificBonusFeature(selectedChannel, selectedShow, selectedBonusFeature);
		downloadItems.AddRange(newDownloadItems);
	}

	if (downloadItems.Count > 0)
	{
		var tempPath = Path.Combine(Path.GetTempPath(), "rt_archive");
		if (Directory.Exists(tempPath) == false)
		{
			Directory.CreateDirectory(tempPath);
		}
		
		Console.WriteLine($"Downloading {downloadItems.Count} items.");
		var lockObject = new Object();

		var parallelOptions = new ParallelOptions()
		{
			MaxDegreeOfParallelism = 4,
		};
		await Parallel.ForEachAsync(downloadItems, async (downloadItem, token) =>
		{
			if (File.Exists(downloadItem.LocalPath))
			{
				Log.Information($"File exist, skipping. {Path.GetFileName(downloadItem.LocalPath)}");
				Console.WriteLine($"File exist, skipping. {Path.GetFileName(downloadItem.LocalPath)}");
				return;
			}
			
			// TODO: Check for m3u8, if its m3u8 save as mp4, otherwise this could be an image.
			var tempFile = Path.Combine(tempPath, Guid.NewGuid().ToString("D"));
			var localFilename = Path.Combine(downloadItem.LocalPath);
			Log.Information($"Downloading {localFilename}");
			Console.WriteLine($"Downloading {localFilename}");
			try
			{
				var processResults = await ProcessEx.RunAsync("yt-dlp", $"-o \"{tempFile}\" --no-progress  --merge-output-format mkv --embed-subs --sub-langs all --write-description --write-info-json --write-thumbnail \"{downloadItem.RemoteManifestPath}\"");
				if (processResults.ExitCode != 0)
				{
					throw new Exception("Download did not complete.");
				}

				lock (lockObject)
				{
					var targetDirectory = Path.GetDirectoryName(downloadItem.LocalPath);
					if (String.IsNullOrEmpty(targetDirectory) == false && Directory.Exists(targetDirectory) == false)
					{
						Directory.CreateDirectory(targetDirectory);
					}
					
					// Our temp file gets mkv added to it, so we need to add that to copy it.
					File.Move(tempFile + ".mkv", downloadItem.LocalPath);
				}

				//Debugger.Break();
			}
			catch (Exception err)
			{
				Log.Error(err, $"Could not download {localFilename}");
				Console.WriteLine($"Error: {err.Message}");
				//Debugger.Break();
			}
		});
	}
	else
	{
		Log.Information("Could not find anything to download.");
		Console.WriteLine("Could not find anything to download.");
	}
	

	
	Debugger.Break();
}*/


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
