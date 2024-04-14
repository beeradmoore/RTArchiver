using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection.Metadata;
using System.Text.Json;
using System.Web;
using System.Xml.Serialization;
using RTArchiver.Data;
using RTArchiver.Data.Requests;
using RTArchiver.Data.Responses;
using RTArchiver.Extensions;
using Serilog;
using Serilog.Core;
using SQLite;

namespace RTArchiver;

public class RTClient
{
	readonly HttpClient _httpClient = new HttpClient();
	AuthResponse? _authResponse;

	public Dictionary<string, Genre> Genres { get; } = new Dictionary<string, Genre>();
	public Dictionary<string, Show> Shows { get; } = new Dictionary<string, Show>();
	public Dictionary<string, Channel> Channels { get; } = new Dictionary<string, Channel>();

	public SQLiteConnection CacheSQLiteConnection { get; init; }

	public RTClient()
	{
		_httpClient.DefaultRequestHeaders.Add("client-id", "4338d2b4bdc8db1239360f28e72f0d9ddb1fd01e7a38fbb07b4b1f4ba4564cc5");
		_httpClient.DefaultRequestHeaders.Add("client-type", "web");
		_httpClient.BaseAddress = new Uri("https://svod-be.roosterteeth.com");

		var authResponse = AuthResponse.Load();
		if (authResponse is not null)
		{
			_authResponse = authResponse;
		}

		CacheSQLiteConnection = new SQLiteConnection(Path.Combine(Storage.DatabasePath, "cache.db"), 
			SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.FullMutex);

		CacheSQLiteConnection.CreateTable<CacheItem>();
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

	object _diskIOLock = new object();
	async Task<TResponse?> GetAPIRequest<TResponse>(string endpoint, int page = 1, bool useAuth = true)
	{
		var guid = Guid.NewGuid().ToString("D");
		var stopwatch = new Stopwatch();
		stopwatch.Start();
		Log.Information($"{guid} GetAPIRequest: {endpoint}, page {page}, useAuth: {useAuth}");
		
		if (endpoint.StartsWith("http"))
		{
			Debugger.Break();
		}
			
		//endpoint += "/sda/213odfs?gfda=dai9&thing=that&tha=dsa";
		
		
		var modifiedQueryArguments = string.Empty;
		var modifiedEndpoint = endpoint;
		var indexOfQueryString = modifiedEndpoint.IndexOf("?", StringComparison.OrdinalIgnoreCase);
		if (indexOfQueryString > 0)
		{
			modifiedQueryArguments = modifiedEndpoint.Substring(indexOfQueryString + 1);
			modifiedEndpoint = modifiedEndpoint.Substring(0, indexOfQueryString);
		}
		
		
		var endpointUrlSplit = modifiedEndpoint.Split("/", StringSplitOptions.RemoveEmptyEntries);
		var cacheDirectory = endpointUrlSplit[0..^1];
		var cacheFileName = $"{endpointUrlSplit[^1]}_page-{page}";

		var queryArguments = HttpUtility.ParseQueryString(modifiedQueryArguments);
		if (queryArguments.Count > 0)
		{
			queryArguments.Remove("page");
			queryArguments.Remove("per_page");
			queryArguments.Remove("order");
			queryArguments.Remove("order_by");
			
			foreach (var nameValueKey in queryArguments.AllKeys)
			{
				if (nameValueKey is null)
				{
					continue;
				}
				
				var nameValueValue = queryArguments[nameValueKey];

				if (string.IsNullOrWhiteSpace(nameValueValue))
				{
					continue;
				}
				
				cacheFileName += $"_{nameValueKey}-{nameValueValue}";
			}
		}

		cacheFileName += ".json";

		var fullCacheDirectory = Path.Combine(Storage.CachePath, string.Join(Path.DirectorySeparatorChar, cacheDirectory));
		var fullCacheFileName = Path.Combine(fullCacheDirectory, cacheFileName);

		var cacheFileExists = File.Exists(fullCacheFileName);

		
		queryArguments.Add("page", page.ToString());
		queryArguments.Add("per_page", "1000");
		queryArguments.Add("order", "desc");

		modifiedQueryArguments = queryArguments.ToString();
		
		Log.Information($"{guid} - {stopwatch.ElapsedMilliseconds} - prepped query");

		Log.Information($"modifiedQueryArguments: {modifiedQueryArguments}");
		
	
		/*
		if (cacheFileExists)
		{
			try
			{
				using (var fileStream = File.OpenRead(fullCacheFileName))
				{
					return JsonSerializer.Deserialize<TResponse>(fileStream);
				}
			}
			catch (Exception err)
			{
				Log.Error(err, $"Could not load request from disk, {fullCacheFileName}");
				Console.WriteLine($"Error: Could not load request from disk, {fullCacheFileName}");
				Console.WriteLine(err.Message);
			}
		}
		*/
		
		var modifiedEndpointWithQuery = (string.IsNullOrWhiteSpace(modifiedQueryArguments) ? modifiedEndpoint : $"{modifiedEndpoint}?{modifiedQueryArguments}");
		Log.Information($"{guid} - {stopwatch.ElapsedMilliseconds} - before request");

		using (var request = new HttpRequestMessage(HttpMethod.Get, modifiedEndpointWithQuery))
		{
			if (useAuth)
			{
				request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _authResponse?.AccessToken ?? string.Empty);
			}
			Log.Information($"{guid} - {stopwatch.ElapsedMilliseconds} - before loading cache row");

			//var cacheItem = CacheSQLiteConnection.Table<CacheItem>().SingleOrDefault(x => x.Endpoint.Equals(modifiedEndpointWithQuery, StringComparison.OrdinalIgnoreCase));
			//var cacheItem = CacheSQLiteConnection.Table<CacheItem>().Where(x => x.Endpoint.Equals(modifiedEndpointWithQuery, StringComparison.OrdinalIgnoreCase)).Take(1);
			var cacheItem = CacheSQLiteConnection.Query<CacheItem>("SELECT * FROM cache_item WHERE endpoint = ? LIMIT 1", modifiedEndpointWithQuery).FirstOrDefault();

			
			Log.Information($"{guid} - {stopwatch.ElapsedMilliseconds} - after loading cache row");

			// Only bother with ETag if cache file exists.
			if (cacheFileExists && string.IsNullOrEmpty(cacheItem?.ETag) == false)
			{
				request.Headers.Add("If-None-Match", cacheItem.ETag);
			}
			
			Log.Information($"{guid} - {stopwatch.ElapsedMilliseconds} - before send async");

			var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
			Log.Information($"{guid} - {stopwatch.ElapsedMilliseconds} - after send async");

			// TODO: GET ETAG HERE
			
			if (response.StatusCode == HttpStatusCode.NotModified)
			{
				try
				{
					Log.Information($"{guid} - {stopwatch.ElapsedMilliseconds} - load from cache");

					using (var fileStream = File.OpenRead(fullCacheFileName))
					{
						var cachedResponseObject = JsonSerializer.Deserialize<TResponse>(fileStream);
						
						// cacheItem should never be null here as we won't be setting ETag if it is null
						if (cacheItem is not null)
						{
							cacheItem.LastChecked = DateTime.Now;
							CacheSQLiteConnection.Update(cacheItem);
						}
						Log.Information($"{guid} - {stopwatch.ElapsedMilliseconds} - loaded from cache");

						return cachedResponseObject;
					}
				}
				catch (Exception err)
				{
					Log.Error(err, $"Could not load request from disk, {fullCacheFileName}");
					Console.WriteLine($"Error: Could not load request from disk, {fullCacheFileName}");
					Console.WriteLine(err.Message);
					Debugger.Break();
				}
				
				// Should never get here.
				Debugger.Break();
			}
			else if (response.StatusCode == HttpStatusCode.NotFound)
			{
				Log.Information($"{guid} GetAPIRequest: response code 404 - {endpoint}");
				return default(TResponse);
			}
			else if (response.StatusCode != HttpStatusCode.OK)
			{
				Debugger.Break();
			}
			
			Log.Information($"{guid} - {stopwatch.ElapsedMilliseconds} - start download");

			using (var memoryStream = new MemoryStream())
			{
				using (var responseStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
				{
					await responseStream.CopyToAsync(memoryStream).ConfigureAwait(false);
				}

				memoryStream.Position = 0;

				Log.Information($"{guid} - {stopwatch.ElapsedMilliseconds} - start save");

				try
				{
					lock (_diskIOLock)
					{
						Directory.CreateDirectory(fullCacheDirectory);
						
						/*
						var cacheDirectoryExists = Directory.Exists(fullCacheDirectory);

						if (cacheDirectoryExists == false)
						{
							Directory.CreateDirectory(fullCacheDirectory);
						}
						*/
					}

					using (var fileStream = File.Create(fullCacheFileName))
					{
						await memoryStream.CopyToAsync(fileStream);
					}
					
					if (response.Headers.ETag != null)
					{
						cacheItem = new CacheItem()
						{
							Endpoint = modifiedEndpointWithQuery,
							ETag = response.Headers.ETag.ToString(),
							LastChecked = DateTime.Now,
						};
				
						CacheSQLiteConnection.InsertOrReplace(cacheItem);
					}
				}
				catch (Exception err)
				{
					Log.Error(err, $"Could not save request to disk, {fullCacheFileName}");
					Console.WriteLine($"Error: Could not save request to disk, {fullCacheFileName}");
					Console.WriteLine(err.Message);
				}
#if DEBUG
				/*
				// Helps debug a specific endpoint as plaintext.
				if (endpoint.Contains("shows", StringComparison.InvariantCultureIgnoreCase))
				{
					//Debugger.Break();
				}
				*/
#endif
				
				memoryStream.Position = 0;
				try
				{
					return await JsonSerializer.DeserializeAsync<TResponse>(memoryStream);
				}
				catch (Exception err)
				{
					Log.Error(err, $"Could not deserialize json for {endpoint}.");
					return default(TResponse);
				}
				finally
				{
					Log.Information($"{guid} - {stopwatch.ElapsedMilliseconds} - done");
				}
			}
		}
	}
	
	internal async Task<(bool Success, List<T> Items)> GetPaginatedAPIRequest<T, TResponse>(string endpoint) where TResponse : BaseResponse<T>
	{
		// No http requests should be coming here
		if (endpoint.StartsWith("http", StringComparison.OrdinalIgnoreCase))
		{
			Debugger.Break();
			return (true, new List<T>());
		}

		Log.Information($"GetPaginatedAPIRequest: {endpoint}");
		
		var retries = 0;
		var maxRetries = 5;
		
		var items = new List<T>();
		var currentPage = 1;
		
		TResponse? response = null;
		do
		{
			try
			{
				response = await GetAPIRequest<TResponse>(endpoint, page: currentPage).ConfigureAwait(false);
				if (response == null)
				{
					throw new Exception("Response object was null");
				}
				
				items.AddRange(response.Data);
				++currentPage;
			}
			catch (Exception err)
			{
				++retries;
				Log.Error(err, $"API Error, retries: {retries}, currentPage: {currentPage}, url: {endpoint}");
				
				// Wait 5 seconds before trying again.
				await Task.Delay(5000);
			}
			
			// Sometimes TotalPages is incorrect, and sometimes TotalResults is incorrect so we have to check both and hope for the best -_-
		} while (retries < maxRetries && currentPage <= response?.TotalPages && items.Count < response?.TotalResults);

		if (retries > maxRetries)
		{
			Log.Error($"Could not get all pages for url {endpoint}");
			return (false, new List<T>());
		}
		
		return (true, items);
	}

	public async Task<MeResponse?> GetMe(bool hasJustRefreshed = false)
	{
		var url2 = "https://business-service.roosterteeth.com/api/v1/me";
		using (var request = new HttpRequestMessage(HttpMethod.Get, url2))
		{
			request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _authResponse?.AccessToken ?? string.Empty);

			var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

			if (response.StatusCode == HttpStatusCode.Unauthorized)
			{
				if (hasJustRefreshed)
				{
					Log.Error("Could not refresh user data.");
					return null;
				}
				else
				{
					// Try to refresh the access token.
					var didRefresh = await RefreshToken();
					if (didRefresh == false)
					{
						Log.Error("Could not refresh user data.");
						return null;
					}

					// Call this again, but lets not get stuck with recursan..
					return await GetMe(true);
				}
			}
			else if (response.StatusCode != HttpStatusCode.OK)
			{
				Log.Error("Could not fetch user data.");
				return null;
			}

			using (var memoryStream = new MemoryStream())
			{
				using (var responseStream = await response.Content.ReadAsStreamAsync())
				{
					await responseStream.CopyToAsync(memoryStream);
				}

				memoryStream.Position = 0;
				try
				{
					return await JsonSerializer.DeserializeAsync<MeResponse>(memoryStream);
				}
				catch (Exception err)
				{
					Log.Error(err, $"Could not deserialize json for {url2}.");
					return default(MeResponse);
				}
			}
		}
	}

	public async Task<List<Genre>> GetGenres()
	{
		Genres.Clear();

		// Load from cache file.
		/*
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
		*/

	
		var response = await GetPaginatedAPIRequest<Genre, GenresResponse>("/api/v1/genres");
		if (response.Success)
		{
			foreach (var genre in response.Items)
			{
				if (Genres.ContainsKey(genre.Slug))
				{
					Log.Error($"Duplicate genre key found, {genre.Slug}");
					Console.WriteLine($"Error: Duplicate genre key found, {genre.Slug}");
				}

				Genres[genre.Slug] = genre;
			}
		}
		else
		{
			Console.WriteLine("Error: Could not load genres.");
			Log.Error("Could not load genres.");
			return new List<Genre>();
		}
		
		// Save to cache file.
		/*
		using (var fileStream = File.Create(genresCacheFile))
		{
			await JsonSerializer.SerializeAsync(fileStream, Genres, new JsonSerializerOptions() { WriteIndented = true });
		}
		*/

		return Genres.Values.ToList();
	}
	
	public async Task<List<Channel>> GetChannels()
	{
		Channels.Clear();
		
		// Load from cache file.
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
		
		var response = await GetPaginatedAPIRequest<Channel, ChannelsResponse>("/api/v1/channels");
		if (response.Success)
		{
			foreach (var channel in response.Items)
			{
				if (Channels.ContainsKey(channel.Slug))
				{
					Log.Error($"Duplicate channel key found, {channel.Slug}");
					Console.WriteLine($"Error: Duplicate channel key found, {channel.Slug}");
				}
				Channels[channel.Slug] = channel;
			}
		}
		else
		{
			Console.WriteLine("Error: Could not load channels.");
			Log.Error("Could not load channels.");
			return new List<Channel>();
		}
		
		// Save file to cache.
		using (var fileStream = File.Create(channelsCacheFile))
		{
			await JsonSerializer.SerializeAsync(fileStream, Channels, new JsonSerializerOptions() { WriteIndented = true });
		}

		return Channels.Values.ToList();
	}

	public async Task<List<Show>> GetShows()
	{
		Shows.Clear();
		
		// Load from cache file
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
		
		
		var response = await GetPaginatedAPIRequest<Show, ShowsResponse>("/api/v1/channels");
		if (response.Success)
		{
			foreach (var show in response.Items)
			{
				if (Shows.ContainsKey(show.Slug))
				{
					Log.Error($"Duplicate show key found, {show.Slug}");
					Console.WriteLine($"Error: Duplicate show key found, {show.Slug}");
				}
				Shows[show.Slug] = show;
			}
		}
		else
		{
			Console.WriteLine("Error: Could not load shows.");
			Log.Error("Could not load shows.");
			return new List<Show>();
		}
		
		// Save to cache file
		using (var fileStream = File.Create(showsCacheFile))
		{
			await JsonSerializer.SerializeAsync(fileStream, Shows, new JsonSerializerOptions() { WriteIndented = true });
		}
		
		return Shows.Values.ToList();
	}
	
	
	public async Task<List<Show>> GetShows(Channel channel)
	{
		var response = await GetPaginatedAPIRequest<Show, ShowsResponse>(channel.Links.Shows);
		if (response.Success)
		{
			return response.Items;
		}
		else
		{
			Console.WriteLine("Error: Could not load shows.");
			Log.Error("Could not load shows.");
			return new List<Show>();
		}
	}

	public async Task<List<Season>> GetSeasons(string showSlug)
	{
		var response = await GetPaginatedAPIRequest<Season, SeasonsResponse>($"https://svod-be.roosterteeth.com/api/v1/shows/{showSlug}/seasons");
		if (response.Success)
		{
			return response.Items;
		}
		else
		{
			Console.WriteLine("Error: Could not load shows.");
			Log.Error("Could not load shows.");
			return new List<Season>();
		}
	}
	
	public async Task<List<Season>> GetSeasons(Show show)
	{
		var response = await GetPaginatedAPIRequest<Season, SeasonsResponse>(show.Links.Seasons);
		if (response.Success)
		{
			return response.Items;
		}
		else
		{
			Console.WriteLine("Error: Could not load shows.");
			Log.Error("Could not load shows.");
			return new List<Season>();
		}
	}
	
	public async Task<List<BonusFeature>> GetBonusFeatures(string showSlug)
	{
		var response = await GetPaginatedAPIRequest<BonusFeature, BonusFeaturesResponse>($"https://svod-be.roosterteeth.com/api/v1/shows/{showSlug}/bonus_features");
		if (response.Success)
		{
			return response.Items;
		}
		else
		{
			Console.WriteLine("Error: Could not load shows.");
			Log.Error("Could not load shows.");
			return new List<BonusFeature>();
		}
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
		var response = await GetPaginatedAPIRequest<Episode, EpisodesResponse>(season.Links.Episodes);
		if (response.Success)
		{
			return response.Items;
		}
		else
		{
			Console.WriteLine("Error: Could not load episodes.");
			Log.Error("Could not load episodes.");
			return new List<Episode>();
		}
	}
	
	public async Task<List<Episode>> GetEpisodes(Channel channel)
	{
		var response = await GetPaginatedAPIRequest<Episode, EpisodesResponse>(channel.Links.Episodes);
		if (response.Success)
		{
			return response.Items;
		}
		else
		{
			Console.WriteLine("Error: Could not load episodes.");
			Log.Error("Could not load episodes.");
			return new List<Episode>();
		}
	}
	
	
	
	public async Task<List<BonusFeature>> GetBonusFeatures(Show show)
	{
		var response = await GetPaginatedAPIRequest<BonusFeature, BonusFeaturesResponse>(show.Links.BonusFeatures);
		if (response.Success)
		{
			return response.Items;
		}
		else
		{
			Console.WriteLine("Error: Could not load bonus features.");
			Log.Error("Could not load bonus features.");
			return new List<BonusFeature>();
		}
	}
	
	public async Task<List<Video>> GetVideos(Episode episode)
	{
		var response = await GetPaginatedAPIRequest<Video, VideosResponse>(episode.Links.Videos);
		if (response.Success)
		{
			return response.Items;
		}
		else
		{
			Console.WriteLine("Error: Could not load videos.");
			Log.Error("Could not load videos.");
			return new List<Video>();
		}
	}
	
	public async Task<List<Video>> GetVideos(BonusFeature bonusFeature)
	{
		
		var response = await GetPaginatedAPIRequest<Video, VideosResponse>(bonusFeature.Links.Videos);
		if (response.Success)
		{
			return response.Items;
		}
		else
		{
			Console.WriteLine("Error: Could not load videos.");
			Log.Error("Could not load videos.");
			return new List<Video>();
		}
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
		var rtClientApiCrawler = new RTClientAPICrawler(this);
		await rtClientApiCrawler.StartAsync();
	}

	public async Task DownloadSitemapsAsync()
	{
		var sitemapSqliteConnection = new SQLiteConnection(Path.Combine(Storage.DatabasePath, "sitemap.db"));
		
		// TODO: Backup DB?
		//_sitemapSqliteConnection.Backup();
		
		try
		{
			sitemapSqliteConnection.CreateTable<Sitemap>();
			sitemapSqliteConnection.CreateTable<StaticPage>();
			sitemapSqliteConnection.CreateTable<VideoUrl>();
			sitemapSqliteConnection.CreateTable<SiteMapVideo>();
		}
		catch (Exception err)
		{
			Log.Error(err, $"Could not create sitemap SQLite database.");
			Debugger.Break();
			return;
		}

		
		var rootSitemap = await DownloadSitemapAsync("https://svod-be.roosterteeth.com/sitemap.xml");

		SitemapIndex? sitemapIndex = null;
		
		using (StreamReader reader = new StreamReader(rootSitemap))
		{
			var serializer = new XmlSerializer(typeof(SitemapIndex));
			sitemapIndex = (SitemapIndex)serializer.Deserialize(reader);
		}

		if (sitemapIndex is null)
		{
			Log.Error("Could not load root sitemap.");
			Debugger.Break();
			return;
		}

		foreach (var sitemap in sitemapIndex.Sitemaps)
		{
			sitemap.Id = Sitemap.GetIdFromLocation(sitemap.Location);
			sitemap.Tag = Sitemap.GetTagFromLocation(sitemap.Location);
			sitemapSqliteConnection.InsertOrReplace(sitemap);
		}
		
		foreach (var sitemap in sitemapIndex.Sitemaps)
		{
			var sitemapPath = await DownloadSitemapAsync(sitemap.Location);
			
			if (string.IsNullOrEmpty(sitemapPath))
			{
				// This error should already have been handled.
				return;
			}
			
			var stopwatch = new Stopwatch();
			stopwatch.Start();
			
			Log.Information($"{sitemap.Tag} - starting");
			
			if (sitemap.Tag == "static-pages")
			{
				StaticPageSet? staticPageSet = null;
				using (StreamReader reader = new StreamReader(sitemapPath))
				{
					var serializer = new XmlSerializer(typeof(StaticPageSet));
					staticPageSet = (StaticPageSet)serializer.Deserialize(reader);
				}

				if (staticPageSet is null)
				{
					Log.Error($"Could not load sitemap - {sitemapPath}");
					Debugger.Break();
					continue;
				}

				Log.Information($"{sitemap.Tag} - Found {staticPageSet.Urls.Count} items");

				var dbStaticPages = sitemapSqliteConnection.Table<StaticPage>().ToList();
				sitemapSqliteConnection.BeginTransaction();
				foreach (var staticPage in staticPageSet.Urls)
				{
					var dbStaticPage = dbStaticPages.SingleOrDefault(x => x.Location.Equals(staticPage.Location, StringComparison.OrdinalIgnoreCase));
					
					if (dbStaticPage is null)
					{
						staticPage.Guid = Guid.NewGuid().ToByteArray();
					}
					else
					{
						staticPage.Guid = dbStaticPage.Guid;
					}
					sitemapSqliteConnection.InsertOrReplace(staticPage);
				}
				
				sitemapSqliteConnection.Commit();
			}
			else if (sitemap.Tag == "rooster-teeth" ||
			         sitemap.Tag == "achievement-hunter" ||
			         sitemap.Tag == "funhaus" ||
			         sitemap.Tag == "death-battle" ||
			         sitemap.Tag == "kinda-funny" ||
			         sitemap.Tag == "friends-of-rt" ||
			         sitemap.Tag == "rwby-universe" ||
			         sitemap.Tag == "red-vs-blue-universe" ||
			         sitemap.Tag == "all-good-no-worries" ||
			         sitemap.Tag == "best-friends-today" ||
			         sitemap.Tag == "inside-gaming" ||
			         sitemap.Tag == "tales-from-the-stinky-dragon" ||
			         sitemap.Tag == "dogbark" ||
			         sitemap.Tag == "f-kface" ||
			         sitemap.Tag == "camp-camp" ||
			         sitemap.Tag == "red-web")
			{
				VideoSet? videoSet = null;
				using (StreamReader reader = new StreamReader(sitemapPath))
				{
					var serializer = new XmlSerializer(typeof(VideoSet));
					videoSet = (VideoSet)serializer.Deserialize(reader);
				}
				
				//var video = videoSet.Urls.First();

				if (videoSet is null)
				{
					Log.Error($"Could not load sitemap - {sitemapPath}");
					Debugger.Break();
					continue;
				}
				
				Log.Information($"{sitemap.Tag} - Found {videoSet.Urls.Count} items");
				
				var dbVideoUrls = sitemapSqliteConnection.Table<VideoUrl>().ToList();
				sitemapSqliteConnection.BeginTransaction();
				foreach (var videoUrl in videoSet.Urls)
				{
					videoUrl.SitemapId = sitemap.Id;
					videoUrl.SitemapTag = sitemap.Tag;
					var dbVideoUrl = dbVideoUrls.SingleOrDefault(x => x.Location.Equals(videoUrl.Location, StringComparison.OrdinalIgnoreCase));
					if (dbVideoUrl is null)
					{
						videoUrl.Guid = Guid.NewGuid().ToByteArray();
					}
					else
					{
						videoUrl.Guid = dbVideoUrl.Guid;
					}
					sitemapSqliteConnection.InsertOrReplace(videoUrl);

					videoUrl.Video.Guid = videoUrl.Guid;
					sitemapSqliteConnection.InsertOrReplace(videoUrl.Video);
				}
				sitemapSqliteConnection.Commit();
			}
			else
			{
				Log.Error($"Unknown sitemap tag found - {sitemap.Tag}");
				Debugger.Break();
			}
			
			stopwatch.Stop();
			Log.Information($"{sitemap.Tag} - Finished, took {stopwatch.ElapsedMilliseconds}ms");
		}
	}
	
	async Task<string> DownloadSitemapAsync(string url)
	{
		Log.Information($"Downloading sitemap - {url}");
		var sitemapFile = url.Replace("https://svod-be.roosterteeth.com/", string.Empty, StringComparison.OrdinalIgnoreCase).Replace("/", "_", StringComparison.OrdinalIgnoreCase);
		var cacheFile = Path.Combine(Storage.CachePath, sitemapFile);

		try
		{
			// TODO: Handle cache.
			/*
			if (File.Exists(cacheFile))
			{
				return cacheFile;
			}
			*/
		
			var response = await _httpClient.GetAsync(url);
			response.EnsureSuccessStatusCode();
			
			using (var fileStream = File.Create(cacheFile))
			{
				using (var stream = await response.Content.ReadAsStreamAsync())
				{
					await stream.CopyToAsync(fileStream);
				}
			}

			return cacheFile;
		}
		catch (Exception err)
		{
			Log.Error(err, $"Could not download sitemap - {url}");
			Debugger.Break();
			return string.Empty;
		}
	}
}
