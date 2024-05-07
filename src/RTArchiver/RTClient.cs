using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.InteropServices.JavaScript;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;
using System.Xml.Serialization;
using RTArchiver.Data;
using RTArchiver.Data.Requests;
using RTArchiver.Data.Responses;
using RTArchiver.Extensions;
using RunProcessAsTask;
using Serilog;
using SQLite;

namespace RTArchiver;

public class RTClient
{
	readonly HttpClient _httpClient = new HttpClient();
	AuthResponse? _authResponse;

	public SQLiteConnection CacheSQLiteConnection { get; init; }

	public int NumberOfThreads { get; set; } = Environment.ProcessorCount;

	public bool UseCache { get; set; } = true;
	
	public RTClient()
	{
		_httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.4.1 Safari/605.1.15");
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
				//ine("Error: Could not get a valid response from the server.");
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
	async Task<(bool Success, int StatusCode, TResponse? Response)> GetAPIRequest<TResponse>(string endpoint, int page = 1, bool useAuth = true, CancellationToken cancellationToken = default(CancellationToken))
	{
		var guid = Guid.NewGuid().ToString("D");
		var stopwatch = new Stopwatch();
		stopwatch.Start();
		Log.Verbose($"{guid} GetAPIRequest: {endpoint}, page {page}, useAuth: {useAuth}");
		
		if (endpoint.StartsWith("http"))
		{
			Log.Error($"GetAPIRequest called with endpoint starting with http, {endpoint}");
			Debugger.Break();
			return (false, 0, default(TResponse));
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
		
		Log.Verbose($"{guid} - {stopwatch.ElapsedMilliseconds} - prepped query");

		Log.Verbose($"modifiedQueryArguments: {modifiedQueryArguments}");
		
	
		
		if (UseCache)
		{
			if (cacheFileExists)
			{
				try
				{
					using (var fileStream = File.OpenRead(fullCacheFileName))
					{
						var tResponse = JsonSerializer.Deserialize<TResponse>(fileStream);
						if (tResponse != null)
						{
							return (true, 200, tResponse);
						}
					}
				}
				catch (Exception err)
				{
					Log.Error(err, $"Could not load request from disk, {fullCacheFileName}");
				}
			}

			return (false, 0, default(TResponse));
		}
		
		var modifiedEndpointWithQuery = (string.IsNullOrWhiteSpace(modifiedQueryArguments) ? modifiedEndpoint : $"{modifiedEndpoint}?{modifiedQueryArguments}");
		Log.Verbose($"{guid} - {stopwatch.ElapsedMilliseconds} - before request");

		using (var request = new HttpRequestMessage(HttpMethod.Get, modifiedEndpointWithQuery))
		{
			if (useAuth)
			{
				request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _authResponse?.AccessToken ?? string.Empty);
			}
			Log.Verbose($"{guid} - {stopwatch.ElapsedMilliseconds} - before loading cache row");

			//var cacheItem = CacheSQLiteConnection.Table<CacheItem>().SingleOrDefault(x => x.Endpoint.Equals(modifiedEndpointWithQuery, StringComparison.OrdinalIgnoreCase));
			//var cacheItem = CacheSQLiteConnection.Table<CacheItem>().Where(x => x.Endpoint.Equals(modifiedEndpointWithQuery, StringComparison.OrdinalIgnoreCase)).Take(1);
			var cacheItem = CacheSQLiteConnection.Query<CacheItem>("SELECT * FROM cache_item WHERE endpoint = ? LIMIT 1", modifiedEndpointWithQuery).FirstOrDefault();

			
			Log.Verbose($"{guid} - {stopwatch.ElapsedMilliseconds} - after loading cache row");

			// Only bother with ETag if cache file exists.
			if (cacheFileExists && string.IsNullOrEmpty(cacheItem?.ETag) == false)
			{
				request.Headers.Add("If-None-Match", cacheItem.ETag);
			}
			
			Log.Verbose($"{guid} - {stopwatch.ElapsedMilliseconds} - before send async");

			var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
			Log.Verbose($"{guid} - {stopwatch.ElapsedMilliseconds} - after send async");
			
			if (response.StatusCode == HttpStatusCode.NotModified)
			{
				try
				{
					Log.Verbose($"{guid} - {stopwatch.ElapsedMilliseconds} - load from cache");

					using (var fileStream = File.OpenRead(fullCacheFileName))
					{
						var cachedResponseObject = JsonSerializer.Deserialize<TResponse>(fileStream);
						
						// cacheItem should never be null here as we won't be setting ETag if it is null
						if (cacheItem is not null)
						{
							cacheItem.LastChecked = DateTime.Now;
							CacheSQLiteConnection.Update(cacheItem);
						}
						Log.Verbose($"{guid} - {stopwatch.ElapsedMilliseconds} - loaded from cache");

						return (true, (int)response.StatusCode, cachedResponseObject);
					}
				}
				catch (Exception err)
				{
					Log.Error(err, $"Could not load request from disk, {fullCacheFileName}");
					Debugger.Break();
				}
				
				// Should never get here.
				Debugger.Break();
			}
			else if (response.StatusCode == HttpStatusCode.NotFound)
			{
				Log.Error($"{guid} GetAPIRequest: response code 404 - {endpoint}");
				return (false, (int)response.StatusCode, default(TResponse));
			}
			else if (response.StatusCode == HttpStatusCode.Unauthorized)
			{
				Log.Error($"{guid} GetAPIRequest: response code 401 - {endpoint}");
				return (false, (int)response.StatusCode, default(TResponse));
			}
			else if (response.StatusCode != HttpStatusCode.OK)
			{
				Log.Error($"{guid} GetAPIRequest: response code {((int)response.StatusCode)} - {endpoint}");
				Debugger.Break();
				return (false, (int)response.StatusCode, default(TResponse));
			}
			
			Log.Verbose($"{guid} - {stopwatch.ElapsedMilliseconds} - start download");

			using (var memoryStream = new MemoryStream())
			{
				using (var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
				{
					await responseStream.CopyToAsync(memoryStream, cancellationToken).ConfigureAwait(false);
				}

				memoryStream.Position = 0;

				Log.Verbose($"{guid} - {stopwatch.ElapsedMilliseconds} - start save");

				try
				{
					lock (_diskIOLock)
					{
						if (Directory.Exists(fullCacheDirectory) == false)
						{
							Directory.CreateDirectory(fullCacheDirectory);
						}
					}

					using (var fileStream = File.Create(fullCacheFileName))
					{
						await memoryStream.CopyToAsync(fileStream).ConfigureAwait(false);
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
					var responseObject = await JsonSerializer.DeserializeAsync<TResponse>(memoryStream);
					return (true, (int)response.StatusCode, responseObject);
				}
				catch (TaskCanceledException err) when (err.CancellationToken == cancellationToken)
				{
					return (false, 0, default(TResponse));
				}
				catch (Exception err)
				{
					Log.Error(err, $"Could not deserialize json for {endpoint}.");
					return (false, (int)response.StatusCode, default(TResponse));
				}
				finally
				{
					Log.Verbose($"{guid} - {stopwatch.ElapsedMilliseconds} - done");
				}
			}
		}
	}
	
	internal async Task<(bool Success, int Pages, int LastStatusCode, List<T> Items)> GetPaginatedAPIRequest<T, TResponse>(string endpoint, CancellationToken cancellationToken = default(CancellationToken)) where TResponse : BaseResponse<T>
	{
		// No http requests should be coming here
		if (endpoint.StartsWith("http", StringComparison.OrdinalIgnoreCase))
		{
			Log.Error($"GetPaginatedAPIRequest called with endpoint starting with http, {endpoint}");
			Debugger.Break();
			return (false, 0, 0, new List<T>());
		}

		Log.Information($"GetPaginatedAPIRequest: {endpoint}");
		
		var retries = 0;
		var maxRetries = 5;
		
		var items = new List<T>();
		var currentPage = 1;

		(bool Success, int StatusCode, TResponse? Response) apiResponse = (false, 0, null);
		do
		{
			try
			{
				apiResponse = await GetAPIRequest<TResponse>(endpoint, page: currentPage, cancellationToken: cancellationToken).ConfigureAwait(false);

				if (cancellationToken.IsCancellationRequested)
				{
					return (false, 0, 0, new List<T>());
				}
				
				if (apiResponse.Success)
				{
					if (apiResponse.Response is not null)
					{
						items.AddRange(apiResponse.Response.Data);
					}
					else
					{
						throw new Exception("Response was success, but response object was null");
					}
				}
				else
				{
					Log.Error($"GetPaginatedAPIRequest: Invalid status code, {apiResponse.StatusCode}");
					return (false, currentPage, apiResponse.StatusCode, items);
				}
				
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
		} while (retries < maxRetries && currentPage <= apiResponse.Response?.TotalPages && items.Count < apiResponse.Response?.TotalResults);

		if (retries > maxRetries)
		{
			Log.Error($"Could not get all pages for url {endpoint}");
			return (false, currentPage, 0, new List<T>());
		}
		
		return (true, currentPage, 200, items);
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
		var cacheDataPath = Path.Combine(Storage.CachePath, "data");
		if (Directory.Exists(cacheDataPath) == false)
		{
			Directory.CreateDirectory(cacheDataPath);
		}
		
		var cacheFile = Path.Combine(cacheDataPath, "genres.json");
		
		// Load from cache file.
		if (UseCache)
		{
			if (File.Exists(cacheFile))
			{
				using (var fileStream = File.OpenRead(cacheFile))
				{
					var tempChannels = JsonSerializer.Deserialize<List<Genre>>(fileStream);
					if (tempChannels != null)
					{
						return tempChannels;
					}
				}
			}
			
			Log.Information("Could not load from cache, requesting from server.");
		}

		var response = await GetPaginatedAPIRequest<Genre, GenresResponse>("/api/v1/genres");
		if (response.Success)
		{
			// Save file to cache.
			using (var fileStream = File.Create(cacheFile))
			{
				await JsonSerializer.SerializeAsync(fileStream, response.Items, new JsonSerializerOptions() { WriteIndented = true });
			}

			return response.Items;
		}
		else
		{
			Log.Error("Could not load channels.");
			return new List<Genre>();
		}
	}
	
	public async Task<List<Channel>> GetChannels()
	{
		var cacheDataPath = Path.Combine(Storage.CachePath, "data");
		if (Directory.Exists(cacheDataPath) == false)
		{
			Directory.CreateDirectory(cacheDataPath);
		}
		
		var cacheFile = Path.Combine(cacheDataPath, "channels.json");
		
		// Load from cache file.
		if (UseCache)
		{
			if (File.Exists(cacheFile))
			{
				using (var fileStream = File.OpenRead(cacheFile))
				{
					var tempChannels = JsonSerializer.Deserialize<List<Channel>>(fileStream);
					if (tempChannels != null)
					{
						return tempChannels;
					}
				}
			}
			
			Log.Information("Could not load from cache, requesting from server.");
		}

		var response = await GetPaginatedAPIRequest<Channel, ChannelsResponse>("/api/v1/channels");
		if (response.Success)
		{
			// Save file to cache.
			using (var fileStream = File.Create(cacheFile))
			{
				await JsonSerializer.SerializeAsync(fileStream, response.Items, new JsonSerializerOptions() { WriteIndented = true });
			}

			return response.Items;
		}
		else
		{
			Log.Error("Could not load channels.");
			return new List<Channel>();
		}
	}

	public async Task<List<Show>> GetShows()
	{
		var cacheDataPath = Path.Combine(Storage.CachePath, "data");
		if (Directory.Exists(cacheDataPath) == false)
		{
			Directory.CreateDirectory(cacheDataPath);
		}
		
		var cacheFile = Path.Combine(cacheDataPath, "shows.json");
		
		// Load from cache file
		if (UseCache)
		{
			if (File.Exists(cacheFile))
			{
				using (var fileStream = File.OpenRead(cacheFile))
				{
					var tempShows = JsonSerializer.Deserialize<List<Show>>(fileStream);
					if (tempShows != null)
					{
						return tempShows;
					}
				}
			}
			
			Log.Information("Could not load from cache, requesting from server.");
		}


		var response = await GetPaginatedAPIRequest<Show, ShowsResponse>("/api/v1/shows");
		if (response.Success)
		{
			// Save to cache file
			using (var fileStream = File.Create(cacheFile))
			{
				await JsonSerializer.SerializeAsync(fileStream, response.Items, new JsonSerializerOptions() { WriteIndented = true });
			}

			return response.Items;
		}
		else
		{
			Console.WriteLine("Error: Could not load shows.");
			Log.Error("Could not load shows.");
			return new List<Show>();
		}
	}
	

	/*
	public async Task<List<Season>> GetSeasons(string showSlug)
	{
		var response = await GetPaginatedAPIRequest<Season, SeasonsResponse>($"/api/v1/shows/{showSlug}/seasons");
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
	*/

	public async Task DownloadSitemapsAsync()
	{
		var sitemapSqliteConnection = new SQLiteConnection(Path.Combine(Storage.SitemapPath, "sitemap.db"));
		
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
			sitemapIndex = serializer.Deserialize(reader) as SitemapIndex;
		}

		if (sitemapIndex is null)
		{
			Log.Error("Could not load root sitemap.");
			Debugger.Break();
			return;
		}

		var parallelOptions = new ParallelOptions()
		{
			MaxDegreeOfParallelism = NumberOfThreads,
		};
		
		Parallel.ForEach(sitemapIndex.Sitemaps, parallelOptions, (sitemap, token) =>
		{
			sitemap.Id = Sitemap.GetIdFromLocation(sitemap.Location);
			sitemap.Tag = Sitemap.GetTagFromLocation(sitemap.Location);
			sitemapSqliteConnection.InsertOrReplace(sitemap);
		});

		var transactionLock = new object();

		await Parallel.ForEachAsync(sitemapIndex.Sitemaps, parallelOptions, async (sitemap, token) =>
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
					staticPageSet = serializer.Deserialize(reader) as StaticPageSet;
				}

				if (staticPageSet is null)
				{
					Log.Error($"Could not load sitemap - {sitemapPath}");
					Debugger.Break();
					return;
				}

				Log.Information($"{sitemap.Tag} - Found {staticPageSet.Urls?.Count} items");

				if (staticPageSet.Urls?.Any() == true)
				{
					var dbStaticPages = sitemapSqliteConnection.Table<StaticPage>().ToList();
					lock (transactionLock)
					{
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
				}
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
					videoSet = serializer.Deserialize(reader) as VideoSet;
				}

				//var video = videoSet.Urls.First();

				if (videoSet is null)
				{
					Log.Error($"Could not load sitemap - {sitemapPath}");
					Debugger.Break();
					return;
				}

				Log.Information($"{sitemap.Tag} - Found {videoSet.Urls?.Count} items");

				if (videoSet.Urls?.Any() == true)
				{
					var dbVideoUrls = sitemapSqliteConnection.Table<VideoUrl>().ToList();

					lock (transactionLock)
					{
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

							if (videoUrl.Video is not null)
							{
								videoUrl.Video.Guid = videoUrl.Guid;
								sitemapSqliteConnection.InsertOrReplace(videoUrl.Video);
							}
						}

						sitemapSqliteConnection.Commit();
					}
				}
			}
			else
			{
				Log.Error($"Unknown sitemap tag found - {sitemap.Tag}");
				Debugger.Break();
			}

			stopwatch.Stop();
			Log.Information($"{sitemap.Tag} - Finished, took {stopwatch.ElapsedMilliseconds}ms");
		});
	}
	
	async Task<string> DownloadSitemapAsync(string url)
	{
		Log.Information($"Downloading sitemap - {url}");
		var sitemapFile = url.Replace("https://svod-be.roosterteeth.com/", string.Empty, StringComparison.OrdinalIgnoreCase).Replace("/", "_", StringComparison.OrdinalIgnoreCase);
		var cacheFile = Path.Combine(Storage.SitemapPath, sitemapFile);

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


	public async Task DownloadImagesAsync()
	{
		var jsonFiles = Directory.GetFiles(Path.Combine(Storage.CachePath, "api", "v1", "watch"), "*.json", SearchOption.AllDirectories);

		var parallelOptions = new ParallelOptions()
		{
			#if DEBUG
			MaxDegreeOfParallelism = 1,
			#else
			MaxDegreeOfParallelism = NumberOfThreads,
			#endif
		};

		var channels = await GetChannels();
		var shows = await GetShows();
		
		channels.Sort();
		shows.Sort();

		foreach (var channel in channels)
		{
			var channelDirectory = Path.Combine(Storage.ImagesPath, channel.Slug);
			if (Directory.Exists(channelDirectory) == false)
			{
				Directory.CreateDirectory(channelDirectory);
			}
		}

		foreach (var show in shows)
		{
			#if DEBUG
			var channelDirectory = Path.Combine(Storage.ImagesPath, show.Attributes.ChannelSlug);
			if (Directory.Exists(channelDirectory) == false)
			{
				// We should never get here. If we do it means the channel is not in the channels list.
				Debugger.Break();
			}
			#endif
			
			var showDirectory = Path.Combine(Storage.ImagesPath, show.Attributes.ChannelSlug, show.Slug);
			if (Directory.Exists(showDirectory) == false)
			{
				Directory.CreateDirectory(showDirectory);
			}
		}
		
		
		async Task DownloadImageAsync(int imageId, string seasonPath, string imageFileNameBase, string imageType, string url)
		{
			if (string.IsNullOrEmpty(url))
			{
				Log.Error($"No url for image {imageId} {imageType}");
				return;
			}
			//var imageFileName = $"{image.Type}_{image.Attributes.ImageType}_{image.Attributes.Orientation}_({image.Id})";

			var extension = Path.GetExtension(url).ToLower(CultureInfo.InvariantCulture);
			var imageFileName = $"{imageFileNameBase}_{imageType}_({imageId}){extension}";
			var tempFileName = Guid.NewGuid().ToString("D") + extension;
			//await DownloadImageAsync(image.Id, seasonDirectory, imageFileName, "small", image.Attributes.Small).ConfigureAwait(false);
			var tempPath = Path.Combine(Storage.TempPath, tempFileName);
			var finalPath = Path.Combine(seasonPath, imageFileName);

			if (File.Exists(finalPath))
			{
				return;
			}
			
			Log.Information($"Downloading {imageId} - {url}");

			try
			{
				using (var response = await _httpClient.GetStreamAsync(url))
				{
					using (var imageStream = File.Create(tempPath))
					{
						await response.CopyToAsync(imageStream).ConfigureAwait(false);
					}
				}

				var fileInfo = new FileInfo(tempPath);
				if (fileInfo.Length == 0)
				{
					throw new Exception("File length is 0");
				}
				
				// TODO: Probably should check if the image can be loaded.

				File.Move(tempPath, finalPath);
			}
			catch (HttpRequestException err) when (err.StatusCode == HttpStatusCode.NotFound)
			{
				// NOOP
				Log.Error($"Image is 404, season: {seasonPath}, url: {url}");
				Debugger.Break();
			}
			catch (Exception err)
			{
				Log.Error(err, $"Could not download image {url}");
			}
		}

		
		var directoryLock = new object();
		
		var knownExtensions = new List<string>() { ".jpg", ".jpeg", ".png", ".gif", ".jp2", ".webp" };
		
		await Parallel.ForEachAsync(jsonFiles, parallelOptions, async (jsonFile, state) =>
		{
			if (Path.GetFileName(jsonFile) == "videos_page-1.json")
			{
				return;
			}
			
			//Log.Information($"Reading: {jsonFile}");
			//var fileData = File.ReadAllText(jsonFile);
			using (var fileStream = File.OpenRead(jsonFile))
			{
				var episodesResponse = await JsonSerializer.DeserializeAsync<EpisodesResponse>(fileStream, cancellationToken: state);

				if (episodesResponse is null)
				{
					Log.Error($"episodesResponse was null");
					Debugger.Break();
					return;
				}

				if (episodesResponse.Data.Count == 0)
				{
					Log.Error($"Zero episodes found - {jsonFile}");
					Debugger.Break();
					return;
				}
		

				if (episodesResponse.Data.Count > 1)
				{
					Log.Error($"More than 1 episodes found - {jsonFile}");
					Debugger.Break();
					return;
				}

				if (episodesResponse.Data[0].Type != "episode" && episodesResponse.Data[0].Type != "bonus_feature")
				{
					Log.Error($"Error: Invalid data type found - {jsonFile}");
					Debugger.Break();
					return;
				}
				
				

				
				Episode episode = episodesResponse.Data[0];

				/*
				if (episode.Type != "bonus_feature")
				{
					return;
				}
				*/
				

				/*
				#if DEBUG
				if (episode.Attributes.SeasonNumber >= 9999)
				{
					Debugger.Break();
				}


				if (episode.Attributes.SeasonNumber <= 0 && episode.Type != "bonus_feature")
				{
					//0782ac10-b6ad-48bb-8a2f-7214e686bed6 kinda funny brave
					// 1b2ff64c-8d0b-4eab-8832-36f6f8f9af0f
					//Debugger.Break();
				}
				#endif
				*/

				var showDirectory = Path.Combine(Storage.ImagesPath, episode.Attributes.ChannelSlug, episode.Attributes.ShowSlug);

				
				BonusFeature? bonusFeature = null;
				if (episode.Type == "bonus_feature")
				{
					fileStream.Position = 0;
					var bonusFeatureResponse = await JsonSerializer.DeserializeAsync<BonusFeaturesResponse>(fileStream, cancellationToken: state);
				
					if (bonusFeatureResponse is null)
					{
						Log.Error($"bonusFeatureResponse was null");
						Debugger.Break();
						return;
					}

					if (bonusFeatureResponse.Data.Count == 0)
					{
						Log.Error($"Zero bonus_feature found - {jsonFile}");
						Debugger.Break();
						return;
					}
	
					if (bonusFeatureResponse.Data.Count > 1)
					{
						Log.Error($"More than 1 bonus_feature found - {jsonFile}");
						Debugger.Break();
						return;
					}

					if (bonusFeatureResponse.Data[0].Type != "bonus_feature")
					{
						Log.Error($"Error: Invalid data type found - {jsonFile}");
						Debugger.Break();
						return;
					}

					bonusFeature = bonusFeatureResponse.Data[0];
					showDirectory = Path.Combine(Storage.ImagesPath, bonusFeature.Attributes.ChannelSlug, bonusFeature.Attributes.ParentContentSlug);
				}
				
		
				if (Directory.Exists(showDirectory) == false)
				{
					lock (directoryLock)
					{
						Directory.CreateDirectory(showDirectory);

					}

					/*
					if (showDirectory.EndsWith("/inside-gaming/inside-gaming-daily") == false &&
					    showDirectory.EndsWith("/inside-gaming/inside-gaming-podcast") == false &&
						showDirectory.EndsWith("/inside-gaming/inside-gaming-reviews") == false &&
						showDirectory.EndsWith("/inside-gaming/inside-gaming-features") == false &&
						showDirectory.EndsWith("/inside-gaming/inside-gaming-explains") == false &&
						showDirectory.EndsWith("/inside-gaming/inside-gaming-live") == false &&
						showDirectory.EndsWith("/inside-gaming/inside-gaming-special")) == false)
					{
						// We should never get here. If we do it means the show is not in the shows list.
						Debugger.Break();
					}
					*/
				}
				
				//var imageFileNames = new List<string>();
				foreach (var image in episode.Included.Images)
				{
					var downloadDirectory = string.Empty;

					if (image.Type == "show_image")
					{
						downloadDirectory = Path.Combine(showDirectory);
					}
					else if (episode.Type == "episode")
					{ 
						downloadDirectory = Path.Combine(showDirectory, episode.Attributes.SeasonSlug, episode.Attributes.Slug);
					}
					else if (episode.Type == "bonus_feature" && bonusFeature is not null)
					{
						downloadDirectory = Path.Combine(showDirectory, "bonus_feature", episode.Attributes.Slug);
						//Debugger.Break();
					}

					if (string.IsNullOrEmpty(downloadDirectory))
					{
						Log.Error($"Could not determine image download directory for {jsonFile}");
					}

					lock (directoryLock)
					{
						if (Directory.Exists(downloadDirectory) == false)
						{
							lock (directoryLock)
							{
								Directory.CreateDirectory(downloadDirectory);
							}
						}
					}

					/*
					var sExtension = Path.GetExtension(image.Attributes.Small).ToLower(CultureInfo.InvariantCulture);
					var mExtension = Path.GetExtension(image.Attributes.Medium).ToLower(CultureInfo.InvariantCulture);
					var lExtension = Path.GetExtension(image.Attributes.Large).ToLower(CultureInfo.InvariantCulture);
					var tExtension = Path.GetExtension(image.Attributes.Thumb).ToLower(CultureInfo.InvariantCulture);

					if (knownExtensions.Contains(sExtension) == false ||
					    knownExtensions.Contains(mExtension) == false ||
					    knownExtensions.Contains(lExtension) == false ||
					    knownExtensions.Contains(tExtension) == false
					   )
					{
						Log.Error("Unknown extension found");
						Debugger.Break();
					}
					*/

					var imageFileName = $"{image.Type}_{image.Attributes.ImageType}_{image.Attributes.Orientation}";
					//var imageFileName = $"{image.Type}_{image.Attributes.ImageType}_{image.Attributes.Orientation}_({image.Id})";
					/*
					if (imageFileNames.Contains(imageFileName))
					{
						Log.Error("ImageFileName exists");
						Debugger.Break();
					}
					else
					{
						imageFileNames.Add(imageFileName);
					}
					*/

					await Task.WhenAll(
						DownloadImageAsync(image.Id, downloadDirectory, imageFileName, "small", image.Attributes.Small),
						DownloadImageAsync(image.Id, downloadDirectory, imageFileName, "medium", image.Attributes.Medium),
						DownloadImageAsync(image.Id, downloadDirectory, imageFileName, "large", image.Attributes.Large),
						DownloadImageAsync(image.Id, downloadDirectory, imageFileName, "thumb", image.Attributes.Thumb)
					);
				}
			}
			
			
			//Debugger.Break();
		});
		
		Debugger.Break();
		
	}

	Regex cleanFileNameRegex = new Regex("([^a-zA-Z0-9# .])");
	
	string CreateCleanFileName(string input)
	{
		var match = cleanFileNameRegex.Match(input);
		if (match.Success == false)
		{
			return input;
		}

		return input;
	}

	public async Task PlaygroundAsync()
	{
		var channels = await GetChannels();
		var shows = await GetShows();

		//x-ray-and-vav"

		channels.Sort();

		Show? show = null;
		
		foreach (var channel in channels)
		{
			//CreateCleanFileName(channel.Name);

			Log.Information($"{channel.Name}");
			var tempShows = new List<Show>();

			foreach (var tempShow in shows)
			{
				if (tempShow.Slug == "x-ray-and-vav")
				{
					show = tempShow;
					break;
				}
			}

			if (show is not null)
			{
				break;
			}
			/*
			tempShows.Sort();

			foreach (var show in tempShows)
			{
				CreateCleanFileName(show.Title);

				Log.Information($" - {show.Title}");
			}
			*/
			

			//Log.Information("\n");


		}
		
		if (show == null)
		{
			return;
		}

		var channelPath = Path.Combine("/Volumes/Storage/plex_playground/data", show.Attributes.ChannelSlug);
		if (Directory.Exists(channelPath) == false)
		{
			Directory.CreateDirectory(channelPath);
		}

		var showPath = Path.Combine(channelPath, show.Slug);
		if (Directory.Exists(showPath) == false)
		{
			Directory.CreateDirectory(showPath);
		}

		if (string.IsNullOrEmpty(show.Links.BonusFeatures) == false)
		{
			var bonusFeaturesResponse = await GetPaginatedAPIRequest<BonusFeature, BonusFeaturesResponse>(show.Links.BonusFeatures);
			if (bonusFeaturesResponse.Success)
			{
				var specialsPath = Path.Combine(showPath, "Specials");
				if (Directory.Exists(specialsPath) == false)
				{
					Directory.CreateDirectory(specialsPath);
				}

				foreach (var bonusFeature in bonusFeaturesResponse.Items)
				{
					var title = bonusFeature.Attributes.Title;
					var summary = bonusFeature.Attributes.Description;
					var slug = bonusFeature.Attributes.Slug;

					var goLiveAt = DateTime.Parse(bonusFeature.Attributes.MemberGoLiveAt);
					var goLiveAtString = TimeZoneInfo.ConvertTime(goLiveAt, TimeZoneInfo.FindSystemTimeZoneById("America/Chicago")); 

					var videoResponse = await GetAPIRequest<VideosResponse>(bonusFeature.Links.Videos);
					if (videoResponse.Success == false || videoResponse.Response is null || videoResponse.Response?.Data.Count != 1)
					{
						Debugger.Break();
						continue;
					}
					
					var video = videoResponse.Response.Data[0];
					//x-ray-and-vav-bonus-3
					var bonusFeatureOutput = Path.Combine(specialsPath, $"{bonusFeature.Attributes.SortNumber:0000}-{bonusFeature.Attributes.Slug}-({video.Id})");

					
					Debugger.Break();

				
				}
				
				


			}


		}
		// /api/v1/shows/x-ray-and-vav/bonus_features
		// /api/v1/shows/x-ray-and-vav/seasons?order=asc&order_by=number
		
		Debugger.Break();
		

		/*

	var jsonFiles = Directory.GetFiles(Path.Combine(Storage.CachePath, "api", "v1", "watch"), "*.json", SearchOption.AllDirectories);

	var parallelOptions = new ParallelOptions()
	{
		MaxDegreeOfParallelism = NumberOfThreads,
	};

	var dictionaryLock = new object();
	*/


		//{channel}/{Show}/{S##}/YYYY-MM-DD-{RTid}/YYYY-MM-DD-{First}{S##E##} Title {RTid}.ext

	}

	
	public async Task DownloadVideosAsync()
	{
		var jsonFiles = Directory.GetFiles(Path.Combine(Storage.CachePath, "api", "v1", "watch"), "videos_*.json", SearchOption.AllDirectories);


		List<string> videos = new List<string>();


		var ids = new Dictionary<long, int>(jsonFiles.Length);
		var contentName = new Dictionary<string, int>(jsonFiles.Length);

		//Debugger.Break();


		var dictionaryLock = new object();

		var parallelOptions = new ParallelOptions()
		{
			MaxDegreeOfParallelism = NumberOfThreads,
		};


		var tempPath = Path.Combine(Storage.TempPath, "rt-archiver");
		if (Directory.Exists(tempPath))
		{
			var tempPathGuid = Path.Combine(Storage.TempPath, $"temp_{Guid.NewGuid().ToString("D")}");

			Directory.Move(tempPath, tempPathGuid);
			Directory.Delete(tempPathGuid, true);
		}
		Directory.CreateDirectory(tempPath);
		await Parallel.ForEachAsync(jsonFiles, parallelOptions, async (jsonFile, state) =>
		{
			Log.Information($"Reading: {jsonFile}");
			//var fileData = File.ReadAllText(jsonFile);
			using (var fileStream = File.OpenRead(jsonFile))
			{
				var videosResponse = await JsonSerializer.DeserializeAsync<VideosResponse>(fileStream, cancellationToken: state);

				if (videosResponse is null)
				{
					Log.Error($"Error: videosResponse was null");
					Debugger.Break();
					return;
				}

				if (videosResponse.Data.Count == 0)
				{
					Log.Error($"Error: Zero videos found - {jsonFile}");
					return;
				}


				if (videosResponse.Data.Count > 1)
				{
					Log.Error($"Error: More than 1 videos found - {jsonFile}");
					Debugger.Break();
					return;
				}

				if (videosResponse.Data[0].Type == "video")
				{
					videos.Add(jsonFile);
				}
				else
				{
					Debugger.Break();
					return;
				}

				var id = videosResponse.Data[0].Id;

				try
				{


					var downloadUrl = videosResponse.Data[0].Links?.Download ?? string.Empty;
					if (string.IsNullOrEmpty(downloadUrl))
					{
						Debugger.Break();
					}

					var tempOutputFile = $"{Guid.NewGuid().ToString("D")}_({id}).mkv";
					var tempOutputPath = Path.Combine(tempPath, tempOutputFile);
					
					var outputFile = $"{id}.mkv";
					var outputPath = Path.Combine(Storage.VideosPath, outputFile);

					if (Path.Exists(outputPath))
					{
						var fileInfo = new FileInfo(outputPath);
						if (fileInfo.Length == 0)
						{
							Log.Error($"File {outputPath} is 0 bytes.");
							return;
						}

						Log.Information($"Skipping {outputPath}");
						return;
					}
					Log.Information($"Downloading {outputFile}");

					var processResults = await ProcessEx.RunAsync("yt-dlp", $"--merge-output-format mkv --embed-subs --sub-langs all --write-description --no-progress --write-info-json --part --concurrent-fragments 8 --check-formats \"{downloadUrl}\" -o \"{tempOutputPath}\"");
					if (processResults.ExitCode == 0)
					{
						var fileInfo = new FileInfo(tempOutputPath);
						if (fileInfo.Length == 0)
						{
							throw new Exception($"File {tempOutputPath} is 0 bytes, not moving.");	
						}
						File.Move(tempOutputPath, outputPath);
					}
					else
					{
						throw new Exception($"Exit code was {processResults.ExitCode}.\n\n{string.Join("\n", processResults.StandardOutput)}\n\n{string.Join("\n", processResults.StandardError)}\n\n");
					}
				}
				catch (Exception err)
				{
					Log.Error(err, $"Could not download video ID {id}, {jsonFile}");
				}
			}
		});
	}
}
