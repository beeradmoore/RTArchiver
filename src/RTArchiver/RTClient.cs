using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using RTArchiver.Data;
using RTArchiver.Data.Requests;
using RTArchiver.Data.Responses;
using RTArchiver.Extensions;
using Serilog;

namespace RTArchiver;

public class RTClient
{
	readonly HttpClient _httpClient = new HttpClient();
	AuthResponse? _authResponse;

	public Dictionary<string, Genre> Genres { get; } = new Dictionary<string, Genre>();
	public Dictionary<string, Show> Shows { get; } = new Dictionary<string, Show>();
	public Dictionary<string, Channel> Channels { get; } = new Dictionary<string, Channel>();
	

	public RTClient()
	{
		_httpClient.DefaultRequestHeaders.Add("client-id", "4338d2b4bdc8db1239360f28e72f0d9ddb1fd01e7a38fbb07b4b1f4ba4564cc5");
		_httpClient.DefaultRequestHeaders.Add("client-type", "web");

		var authResponse = AuthResponse.Load();
		if (authResponse is not null)
		{
			_authResponse = authResponse;
		}
	}


	public bool IsLoggedIn()
	{
		return _authResponse is not null;
	}

	public async Task<bool> Login(string username, string password)
	{
		var authRequest = new AuthRequest { Username = username, Password = password };

		try
		{
			var response = await _httpClient.PostAsJsonAsync("https://auth.roosterteeth.com/oauth/token", authRequest);
			var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>();
			if (authResponse == null)
			{
				Log.Error("Could not get a valid response from the server.");
				Console.WriteLine("Error: Could not get a valid response from the server.");
				Logout();
				return false;
			}

			if (string.IsNullOrEmpty(authResponse.Error) == false)
			{
				Log.Error($"Could not log in. ({authResponse.Error})");
				Console.WriteLine($"Error: Could not log in. ({authResponse.Error})");
				Console.WriteLine(authResponse.ErrorDescription);
				Console.WriteLine(authResponse.ExtraInfo);
				Logout();
				return false;
			}

			_authResponse = authResponse;
			_authResponse.Save();
			return true;
		}
		catch (Exception err)
		{
			Log.Error(err, $"Could not log in");
			Console.WriteLine("Error: Could not log in.");
			Console.WriteLine(err.Message);
			Logout();
			return false;
		}
	}

	public void Logout()
	{
		AuthResponse.Delete();
		_authResponse = null;
	}

	public async Task<bool> RefreshToken()
	{
		var refreshRequest = new RefreshRequest { RefreshToken = _authResponse?.RefreshToken ?? string.Empty };
		try
		{
			var response = await _httpClient.PostAsJsonAsync("https://auth.roosterteeth.com/oauth/token", refreshRequest);
			var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>();
			if (authResponse == null)
			{
				Log.Error("Could not get a valid response from the server.");
				Console.WriteLine("Error: Could not get a valid response from the server.");
				Logout();
				return false;
			}

			if (string.IsNullOrEmpty(authResponse.Error) == false)
			{
				Log.Error($"Could not log in. ({authResponse.Error})");
				Console.WriteLine($"Error: Could not log in. ({authResponse.Error})");
				Console.WriteLine(authResponse.ErrorDescription);
				Console.WriteLine(authResponse.ExtraInfo);
				Logout();
				return false;
			}

			_authResponse = authResponse;
			_authResponse.Save();
			return true;
		}
		catch (Exception err)
		{
			Log.Error(err, $"Could not log in");
			Console.WriteLine("Error: Could not log in.");
			Console.WriteLine(err.Message);
			Logout();
			return false;
		}
	}

	/*
	async Task<TResponse?> GetAllAPIRequest<TResponse>(string url) where TResponse : VideosResponse, BonusFeaturesResponse
	{
		var page = 1;
		TResponse? firstResponse = default(TResponse);
		TResponse response;
		do
		{
			response = await GetAPIRequest<TResponse>(url, page, 5);
			if (firstResponse == null)
			{
				firstResponse = response;
			}
			else
			{
				firstResponse.Data.AddRange(response.Data);
			}


			++page;
		} while (page <= response?.TotalPages);
		

		return firstResponse;
	}
	*/

	async Task<TResponse?> GetAPIRequest<TResponse>(string url, int page = 1, int perPage = 1000, string order = "asc", bool useAuth = true)
	{
		// Some api endpoints are just /api/v1/doStuff, so if one gets past here we add the svod-be address.
		if (url.StartsWith("/api/v1/"))
		{
			url = $"https://svod-be.roosterteeth.com{url}";
		}
		
		var shouldCache = false;
		
		if (url.StartsWith("https://svod-be.roosterteeth.com"))
		{
			if (url.Contains("?", StringComparison.InvariantCultureIgnoreCase))
			{
				url += "&";
			}
			else
			{
				url += "?";
			}

			url += $"per_page={perPage}&page={page}&order={order}";
			shouldCache = true;
		}

		var uri = new Uri(url);

		var requestCacheFile = uri.AbsolutePath;
		if (requestCacheFile.StartsWith("/"))
		{
			requestCacheFile = requestCacheFile.Substring(1);
		}
		requestCacheFile += $"_page{page.ToString().PadLeft(2, '0')}.json";
		
		var requestCachePath = Path.Combine(Storage.CachePath, requestCacheFile);

		if (shouldCache)
		{
			try
			{
				if (File.Exists(requestCachePath))
				{
					using (var fileStream = File.OpenRead(requestCachePath))
					{
						return JsonSerializer.Deserialize<TResponse>(fileStream);
					}
				}
			}
			catch (Exception err)
			{
				Log.Error(err, $"Could not load request from disk, {requestCacheFile}");
				Console.WriteLine($"Error: Could not load request from disk, {requestCacheFile}");
				Console.WriteLine(err.Message);
			}
		}
		//Console.WriteLine(url);

		using (var request = new HttpRequestMessage(HttpMethod.Get, uri))
		{
			if (useAuth)
			{
				request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _authResponse?.AccessToken ?? string.Empty);
			}

			var response = await _httpClient.SendAsync(request);

			// TODO: Check http status. May need to handle auth responses here for refreshing access token.

			using (var responseStream = await response.Content.ReadAsStreamAsync())
			{
				if (shouldCache)
				{
					try
					{
						var directory = Path.GetDirectoryName(requestCachePath);
						if (string.IsNullOrEmpty(directory) == false && Directory.Exists(directory) == false)
						{
							Directory.CreateDirectory(directory);
						}
						using (var fileStream = File.Create(requestCachePath))
						{
							await responseStream.CopyToAsync(fileStream);
						}
					}
					catch (Exception err)
					{
						Log.Error(err, $"Could not save request to disk, {requestCacheFile}");
						Console.WriteLine($"Error: Could not save request to disk, {requestCacheFile}");
						Console.WriteLine(err.Message);
					}
				}
#if DEBUG
				// Helps debug a specific endpoint as plaintext.
				if (url.Contains("shows", StringComparison.InvariantCultureIgnoreCase))
				{
					//Debugger.Break();
				}
#endif

				responseStream.Position = 0;
				return await JsonSerializer.DeserializeAsync<TResponse>(responseStream);
			}
		}
	}

	public async Task<MeResponse?> GetMe()
	{
		return await GetAPIRequest<MeResponse>("https://business-service.roosterteeth.com/api/v1/me");
	}

	public async Task<List<Genre>> GetGenres()
	{
		Genres.Clear();
		
		var genresCacheFile = Path.Combine(Storage.CachePath, "genres.json");
		if (File.Exists(genresCacheFile))
		{
			using (var fileStream = File.OpenRead(genresCacheFile))
			{
				var tempGenres = JsonSerializer.Deserialize<Dictionary<string, Genre>>(fileStream);
				if (tempGenres != null)
				{
					foreach (var tempGenre in tempGenres)
					{
						Genres[tempGenre.Key] = tempGenre.Value;
					}

					return Genres.Values.ToList();
				}
			}
		}
		
		var genresResponse = await GetAPIRequest<GenresResponse>("https://svod-be.roosterteeth.com/api/v1/genres");
		if (genresResponse is not null)
		{
			foreach (var genre in genresResponse.Data)
			{
				if (Genres.ContainsKey(genre.Slug))
				{
					Log.Error($"Duplicate genre key found, {genre.Slug}");
					Console.WriteLine($"Error: Duplicate genre key found, {genre.Slug}");
				}

				Genres[genre.Slug] = genre;
			}
		}
		
		using (var fileStream = File.Create(genresCacheFile))
		{
			await JsonSerializer.SerializeAsync(fileStream, Genres, new JsonSerializerOptions() { WriteIndented = true });
		}

		return Genres.Values.ToList();
	}
	
	public async Task<List<Channel>> GetChannels()
	{
		Channels.Clear();
		
		var channelsCacheFile = Path.Combine(Storage.CachePath, "channels.json");
		if (File.Exists(channelsCacheFile))
		{
			using (var fileStream = File.OpenRead(channelsCacheFile))
			{
				var tempChannels = JsonSerializer.Deserialize<Dictionary<string, Channel>>(fileStream);
				if (tempChannels != null)
				{
					foreach (var tempChannel in tempChannels)
					{
						Channels[tempChannel.Key] = tempChannel.Value;
					}

					return Channels.Values.ToList();
				}
			}
		}
		
		var channels  = new List<Channel>();

		var page = 1;
		ChannelsResponse? channelsResponse;
		do
		{
			// TODO: Add attempts and if a request fails and returns null try attempt again.
			channelsResponse = await GetAPIRequest<ChannelsResponse>("https://svod-be.roosterteeth.com/api/v1/channels", page, useAuth: false);

			//Console.WriteLine($"Loaded page {page}, page: {channelsResponse.Page}, perPage: {channelsResponse.PerPage}, totalPages: {channelsResponse.TotalPages}, totalResults: {channelsResponse.TotalResults}");
			if (channelsResponse != null)
			{
				channels.AddRange(channelsResponse.Data);
				++page;
			}

			// TotalPages appears to change over time, but this should still work ok. 
		} while (page - 1 < channelsResponse?.TotalPages);

		foreach (var channel in channels)
		{
			if (Channels.ContainsKey(channel.Slug))
			{
				Log.Error($"Duplicate channel key found, {channel.Slug}");
				Console.WriteLine($"Error: Duplicate channel key found, {channel.Slug}");
			}
			Channels[channel.Slug] = channel;
		}
		
		using (var fileStream = File.Create(channelsCacheFile))
		{
			await JsonSerializer.SerializeAsync(fileStream, Channels, new JsonSerializerOptions() { WriteIndented = true });
		}

		return channels;
	}

	public async Task<List<Show>> GetShows()
	{
		Shows.Clear();
		
		var showsCacheFile = Path.Combine(Storage.CachePath, "shows.json");
		if (File.Exists(showsCacheFile))
		{
			using (var fileStream = File.OpenRead(showsCacheFile))
			{
				var tempShows = JsonSerializer.Deserialize<Dictionary<string, Show>>(fileStream);
				if (tempShows != null)
				{
					foreach (var tempShow in tempShows)
					{
						Shows[tempShow.Key] = tempShow.Value;
					}

					return Shows.Values.ToList();
				}
			}
		}
		
		var shows = new List<Show>();

		var page = 1;
		ShowsResponse? showsResponse;
		do
		{
			// TODO: Add attempts and if a request fails and returns null try attempt again.
			showsResponse = await GetAPIRequest<ShowsResponse>("https://svod-be.roosterteeth.com/api/v1/shows", page);

			//Console.WriteLine($"Loaded page {page}, page: {showsResponse.Page}, perPage: {showsResponse.PerPage}, totalPages: {showsResponse.TotalPages}, totalResults: {showsResponse.TotalResults}");
			if (showsResponse != null)
			{
				shows.AddRange(showsResponse.Data);
				++page;
			}

			// TotalPages appears to change over time, but this should still work ok. 
		} while (page - 1 < showsResponse?.TotalPages);

		foreach (var show in shows)
		{
			if (Shows.ContainsKey(show.Slug))
			{
				Log.Error($"Duplicate show key found, {show.Slug}");
				Console.WriteLine($"Error: Duplicate show key found, {show.Slug}");
			}

			Shows[show.Slug] = show;
		}
		
		using (var fileStream = File.Create(showsCacheFile))
		{
			await JsonSerializer.SerializeAsync(fileStream, Shows, new JsonSerializerOptions() { WriteIndented = true });
		}
		
		return shows;
	}
	
	
	public async Task<List<Show>> GetShows(Channel channel)
	{
		var shows = new List<Show>();
		var showsResponse = await GetAPIRequest<ShowsResponse>(channel.Links.Shows);
		if (showsResponse?.Data != null)
		{
			shows.AddRange(showsResponse.Data);
		}
		return shows;
	}

	public async Task<List<Season>> GetSeasons(string showSlug)
	{
		var seasonsResponse = await GetAPIRequest<SeasonsResponse>($"https://svod-be.roosterteeth.com/api/v1/shows/{showSlug}/seasons");
		return seasonsResponse?.Data ?? new List<Season>();
	}
	
	public async Task<List<Season>> GetSeasons(Show show)
	{
		var seasonsResponse = await GetAPIRequest<SeasonsResponse>(show.Links.Seasons);
		return seasonsResponse?.Data ?? new List<Season>();
	}
	
	public async Task<List<BonusFeature>> GetBonusFeatures(string showSlug)
	{
		var bonusFeaturesResponse = await GetAPIRequest<BonusFeaturesResponse>($"https://svod-be.roosterteeth.com/api/v1/shows/{showSlug}/bonus_features");
		return bonusFeaturesResponse?.Data ?? new List<BonusFeature>();
	}
		
	public async Task<List<DownloadItem>> GetDownloadItemsForEverything(Channel channel, Show show)
	{
		var downloadItems = new List<DownloadItem>();
		downloadItems.AddRange(await GetDownloadItemsForAllSeasons(channel, show));
		downloadItems.AddRange(await GetDownloadItemsForAllBonusFeatures(channel, show));
		return downloadItems;
	}

	public async Task<List<Episode>> GetEpisodes(Season season)
	{
		// TODO: pagination?
		var episodes = new List<Episode>();
		var episodeResponse = await GetAPIRequest<EpidsodesResponse>(season.Links.Episodes);
		if (episodeResponse?.Data != null)
		{
			episodes.AddRange(episodeResponse.Data);
		}

		return episodes;
	}
	
	public async Task<List<Episode>> GetEpisodes(Channel channel)
	{
		// TODO: pagination?
		var episodes = new List<Episode>();

		var page = 1;
		EpidsodesResponse episodeResponse;
		do
		{
			episodeResponse = await GetAPIRequest<EpidsodesResponse>(channel.Links.Episodes, page: page);
			if (episodeResponse?.Data != null)
			{
				episodes.AddRange(episodeResponse.Data);
			}

			++page;
		} while (page - 1 < episodeResponse?.TotalPages);
		
		return episodes;
	}
	
	
	
	public async Task<List<BonusFeature>> GetBonusFeatures(Show show)
	{
		// TODO: pagination?
		var bonusFeatures = new List<BonusFeature>();
		var bonusFeaturesResponse = await GetAPIRequest<BonusFeaturesResponse>(show.Links.BonusFeatures);
		if (bonusFeaturesResponse?.Data != null)
		{
			bonusFeatures.AddRange(bonusFeaturesResponse.Data);
		}

		return bonusFeatures;
	}
	
	public async Task<List<Video>> GetVideos(Episode episode)
	{
		// TODO: pagination?
		var videos = new List<Video>();
		var videosResponse = await GetAPIRequest<VideosResponse>(episode.Links.Videos);
		if (videosResponse?.Data != null)
		{
			videos.AddRange(videosResponse.Data);
		}

		return videos;
	}
	
	public async Task<List<Video>> GetVideos(BonusFeature bonusFeature)
	{
		// TODO: pagination?
		var videos = new List<Video>();
		var videosResponse = await GetAPIRequest<VideosResponse>(bonusFeature.Links.Videos);
		if (videosResponse?.Data != null)
		{
			videos.AddRange(videosResponse.Data);
		}

		return videos;
	}
	
	
	
	
	public async Task<List<DownloadItem>> GetDownloadItemsForAllSeasons(Channel channel, Show show)
	{
		var downloadItems = new List<DownloadItem>();

		var seasons = await GetSeasons(show.Slug);
		foreach (var season in seasons)
		{
			var episodes = await GetEpisodes(season);
			foreach (var episode in episodes)
			{
				var videos = await GetVideos(episode);
				foreach (var video in videos)
				{
					downloadItems.Add(new DownloadItem()
					{
						RemoteManifestPath = video.Links.Download,
						LocalPath = episode.FullLocalPath(),
					});
				}
			}
		}
		
		return downloadItems;
	}
	
	
	public async Task<List<DownloadItem>> GetDownloadItemsForSpecificSeason(Channel channel, Show show, Season season)
	{
		var downloadItems = new List<DownloadItem>();
		
		var episodes = await GetEpisodes(season);
		foreach (var episode in episodes)
		{
			var videos = await GetVideos(episode);
			foreach (var video in videos)
			{
				downloadItems.Add(new DownloadItem()
				{
					RemoteManifestPath = video.Links.Download,
					LocalPath = episode.FullLocalPath(),
				});
			}
		}
		
		return downloadItems;
	}
	
	
	public async Task<List<DownloadItem>> GetDownloadItemsForAllBonusFeatures(Channel channel, Show show)
	{
		var downloadItems = new List<DownloadItem>();
		
		var bonusFeatures = await GetBonusFeatures(show);
		foreach (var bonusFeature in bonusFeatures)
		{
			var videos = await GetVideos(bonusFeature);
			foreach (var video in videos)
			{
				downloadItems.Add(new DownloadItem()
				{
					RemoteManifestPath = video.Links.Download,
					LocalPath = bonusFeature.FullLocalPath(show),
				});
			}
		}
		return downloadItems;
	}
	
	
	public async Task<List<DownloadItem>> GetDownloadItemsForSpecificBonusFeature(Channel channel, Show show, BonusFeature bonusFeature)
	{
		var downloadItems = new List<DownloadItem>();
		var videos = await GetVideos(bonusFeature);
		foreach (var video in videos)
		{
			downloadItems.Add(new DownloadItem()
			{
				RemoteManifestPath = video.Links.Download,
				LocalPath = bonusFeature.FullLocalPath(show),
			});
		}
		return downloadItems;
	}
	
	
	// TODO: Handle these APIs, set useAuth when its not required 
	// https://svod-be.roosterteeth.com/api/v1/channels (noauth)
	// https://svod-be.roosterteeth.com/api/v1/shows?per_page=50&order=desc&page=1

	// Other samples
	// https://svod-be.roosterteeth.com/api/v1/shows/camp-camp
	// Some of these have bonus features in it as well, we should expose those.
	// https://roosterteeth.com/episodes?channel_id=red-vs-blue-universe
	// https://roosterteeth.com/watch/red-vs-blue-season-4-episode-58

	public async Task CacheGoBrrrr()
	{
		/*
		var response = await GetAllAPIRequest<BaseResponse<object>>("https://svod-be.roosterteeth.com/api/v1/shows/camp-camp");
		Debugger.Break();
		*/
		
		foreach (var channel in Channels.Values)
		{
			
			
			// Disabled as this is a lot of videos
			var allEpisodes = await GetEpisodes(channel);
			foreach (var episode in allEpisodes)
			{
				Console.WriteLine($" -> {episode.FileName()}");
			}
			

			Console.WriteLine($"Channel: {channel.Name}");
			Console.WriteLine("Shows:");
			var shows = await GetShows(channel);
			foreach (var show in shows)
			{
				Console.WriteLine($" -> {show.Attributes.Title}");
				
				Console.WriteLine(" Seasons:");
				var seasons = await GetSeasons(show);
				foreach (var season in seasons)
				{
					Console.WriteLine($"  -> {season.Attributes.Number} - {season.Attributes.Title}");
					
					Console.WriteLine("  Episodes:");
					var episodes = await GetEpisodes(season);
					foreach (var episode in episodes)
					{
						Console.WriteLine($"   -> {episode.FullLocalPath()}");
						var videos = await GetVideos(episode);
						Console.WriteLine($"    -> {videos.Count}");
					}
				}
				
				Console.WriteLine(" Bonus features:");
				var bonusFeatures = await GetBonusFeatures(show);
				foreach (var bonusFeature in bonusFeatures)
				{
					Console.WriteLine($"  -> {bonusFeature.Attributes.Title}");
					
					
					Console.WriteLine($"   -> {bonusFeature.FullLocalPath(show)}");
					var videos = await GetVideos(bonusFeature);
					Console.WriteLine($"    -> {videos.Count}");
					
					//bonusFeature.Links.Videos
				}
			}
			
		}
		
	}
}