using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Web;
using RTArchiver.Data;
using RTArchiver.Data.Responses;
using Serilog;
using Serilog.Core;

namespace RTArchiver;

public class RTClientCommentsCrawler
{
	RTClient _rtClient;

	static object _dictionaryLock = new object();

	List<Thread> _threads = new List<Thread>();

	object _diskIOLock = new object();
	// Used to track what has already bene cached so we don't need to cache it again.
	readonly Dictionary<string, int> _cachedLinksQueueReference = new Dictionary<string, int>();
	ConcurrentBag<string> _episodesToFetch = new ConcurrentBag<string>();

	
	public RTClientCommentsCrawler(RTClient rtClient)
	{
		_rtClient = rtClient;
		
		// main video
		// https://comments.roosterteeth.com/api/v2/topics/{episode_uuid}/comments

		// thread
		// https://comments.roosterteeth.com/api/v2/comments/{comment_uuid}/thread?per_page=1000

	}
	
	public async Task StartAndWaitAsync()
	{
		Log.Information($"Starting: RTClientCommentsCrawler with {_rtClient.NumberOfThreads} threads.");

		var jsonFiles = Directory.GetFiles(Path.Combine(Storage.CachePath, "api", "v1"), "episodes_page-*.json", SearchOption.TopDirectoryOnly);
   
		var parallelOptions = new ParallelOptions()
		{
		   	MaxDegreeOfParallelism = 4,
		};
		
		   		
		//https://comments.roosterteeth.com/api/v2/topics/{episode_uuid}/comments?per_page=100&page=1

		var tempList = new List<string>();
		var listLock = new object();
		
		
		await Parallel.ForEachAsync(jsonFiles, parallelOptions, async (jsonFile, state) =>
		{
			EpisodesResponse? episodesResponse = null;
			using (var fileStream = File.OpenRead(jsonFile))
			{
				episodesResponse = await JsonSerializer.DeserializeAsync<EpisodesResponse>(fileStream, cancellationToken: state).ConfigureAwait(false);
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
			}

			lock (listLock)
			{
				foreach (var episode in episodesResponse.Data)
				{
					tempList.Add(episode.Uuid);
				}
			}
		});
		
		_episodesToFetch = new ConcurrentBag<string>(tempList);
		
		Log.Information($"Found {tempList.Count} episodes to fetch");
		
		var tasks = new List<Task>();
		var cancellationTokenSource = new CancellationTokenSource();
		
		// These endpoints don't like a lot of traffic, so they are rate limited. So we just use 1 thread.
		for (var threadNumber = 0; threadNumber < 1; ++threadNumber)
		{
			tasks.Add(CommentCrawlerWorker(threadNumber, cancellationTokenSource.Token));
		}
		
		
		var isRunning = true;
		do
		{
#if DEBUG
			if (Console.KeyAvailable)
			{
				var key = Console.ReadKey(true);
				if (key.KeyChar == 'q' || key.KeyChar == 'Q')
				{
					Log.Information("Stopping crawlers.");
					cancellationTokenSource.Cancel();
					break;
				}
			}
			else
#endif
			{
				Thread.Sleep(5000);

				// If all tasks are completed then we should exit this while loop.
				if (tasks.All(t => t.IsCompleted))
				{
					isRunning = false;
				}
			}
		} while (isRunning);
	}
	
	async Task CommentCrawlerWorker(int threadNumber, CancellationToken cancellationToken)
	{
		Log.Information($"Starting worker: {threadNumber}");
		try
		{
			// Used to create a random sleep duration.
			var random = new Random();
			var failedToFetchCount = 0;
			do
			{
				if (cancellationToken.IsCancellationRequested)
				{
					break;
				}

				Log.Information($"_episodesToFetch.Count: {_episodesToFetch.Count}");

				var didLoad = false;

				// Try to get an item from the _apisToFetch list, if we can't sleep until we failed to get something 20 times in a row.
				if (_episodesToFetch.TryTake(out string? episodeUuid) == true)
				{
					try
					{
						await ProcessEpisodeAsync(episodeUuid, cancellationToken).ConfigureAwait(false);
						failedToFetchCount = 0;
						didLoad = true;
					}
					catch (Exception err)
					{
						++failedToFetchCount;
						Log.Error(err, $"Failed to fetch {episodeUuid}, adding it back into the list.");
						// Didn't fetch this so we add it back in.
						// There is potential for an endless loop here though...
						_episodesToFetch.Add(episodeUuid);
						Debugger.Break();
					}
				}
				else
				{
					++failedToFetchCount;
				}

				if (didLoad == false)
				{
					if (_episodesToFetch.Count == 0)
					{
						// This means we have failed to have any items added to the list for 10 seconds, so we are likely done.
						if (failedToFetchCount > 5 && _episodesToFetch.Count == 0)
						{
							Log.Information($"Thread has failed to find any item {failedToFetchCount} times. Aborting.");
							return;
						}
						// Wait 2 seconds and try again
						await Task.Delay(2000, cancellationToken).ConfigureAwait(false);
					}
					else
					{
						if (failedToFetchCount > 20)
						{
							Log.Information($"Thread has failed to find any item {failedToFetchCount} times. Aborting.");
							return;
						}
						
						// Wait 1-9.999 seconds and try again
						await Task.Delay(random.Next(1000, 9999), cancellationToken).ConfigureAwait(false);
					}
				}

			} while (true);
		}
		catch (TaskCanceledException err) when (err.CancellationToken == cancellationToken)
		{
			// NOOP - our own cancellation token.
		}
		catch (Exception err)
		{
			Log.Error(err, $"Error in worker: {threadNumber}");
			Debugger.Break();
		}
		finally
		{
			Log.Information($"Exiting worker: {threadNumber}");
		}
		
		Log.Information($"Ending working: {threadNumber}");
	}

	async Task ProcessEpisodeAsync(string episodeUuid, CancellationToken cancellationToken)
	{
		Log.Information($"Processing {episodeUuid}");
		var response = await GetHttpPaginatedAPIRequest<Comment, CommentsResponse>($"https://comments.roosterteeth.com/api/v2/topics/{episodeUuid}/comments", episodeUuid, cancellationToken).ConfigureAwait(false);
		if (response.Success)
		{
			foreach (var comment in response.Items)
			{
				if (comment.ChildCommentsTotalCount > 0)
				{
					var threadResponse = await GetHttpPaginatedAPIRequest<Comment, CommentsResponse>($"https://comments.roosterteeth.com/api/v2/comments/{comment.Uuid}/thread", Path.Combine(episodeUuid, comment.Uuid), cancellationToken).ConfigureAwait(false);
					if (threadResponse.Success == false)
					{
						Log.Error($"Invalid status code {threadResponse.LastStatusCode} for thread {comment.Uuid}");
					}
				}
			}
		}
		else
		{
			// If we failed we stop.
			Log.Error($"Invalid status code {response.LastStatusCode} for episode {episodeUuid}");
		}
	}
	
	async Task<(bool Success, int Pages, int LastStatusCode, List<T> Items)> GetHttpPaginatedAPIRequest<T, TResponse>(string url, string episodeUuid, CancellationToken cancellationToken = default(CancellationToken)) where TResponse : BaseResponse<T>
	{
		Log.Information($"GetHttpPaginatedAPIRequest: {url}");
		
		var retries = 0;
		var maxRetries = 5;
		
		var items = new List<T>();
		var currentPage = 1;

		var episidePath = Path.Combine(Storage.CommentsPath, episodeUuid);
		if (Directory.Exists(episidePath) == false)
		{
			Directory.CreateDirectory(episidePath);
		}
		
		(bool Success, int StatusCode, TResponse? Response) apiResponse = (false, 0, null);
		do
		{
			try
			{
				apiResponse = await GetHttpAPIRequest<TResponse>(url, episodeUuid, episidePath, page: currentPage, cancellationToken: cancellationToken).ConfigureAwait(false);

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
					Log.Error($"GetHttpPaginatedAPIRequest: Invalid status code, {apiResponse.StatusCode}");
					return (false, currentPage, apiResponse.StatusCode, items);
				}
				
				++currentPage;
			}
			catch (Exception err)
			{
				++retries;
				Log.Error(err, $"API Error, retries: {retries}, currentPage: {currentPage}, url: {url}");
				
				// Wait 5 seconds before trying again.
				await Task.Delay(5000);
			}
			
			// Sometimes TotalPages is incorrect, and sometimes TotalResults is incorrect so we have to check both and hope for the best -_-
		} while (retries < maxRetries && currentPage <= apiResponse.Response?.TotalPages && items.Count < apiResponse.Response?.TotalResults);

		if (retries > maxRetries)
		{
			Log.Error($"Could not get all pages for url {url}");
			return (false, currentPage, 0, new List<T>());
		}
		
		return (true, currentPage, 200, items);
	}

	
	async Task<(bool Success, int StatusCode, TResponse? Response)> GetHttpAPIRequest<TResponse>(string url, string episodeUuid, string episidePath, int page = 1, int perPage = 100, bool useAuth = false, CancellationToken cancellationToken = default(CancellationToken))
	{
		var guid = Guid.NewGuid().ToString("D");

		Log.Verbose($"{guid} GetAPIRequest: {url}, page {page}, useAuth: {useAuth}");
		
		var modifiedQueryArguments = string.Empty;
		var modifiedEndpoint = url;
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

		//var fullCacheDirectory = Path.Combine(cachePath.CachePath, string.Join(Path.DirectorySeparatorChar, cacheDirectory));
		var fullCacheFileName = Path.Combine(episidePath, cacheFileName);

		var cacheFileExists = File.Exists(fullCacheFileName);

		
		queryArguments.Add("page", page.ToString());
		queryArguments.Add("per_page", perPage.ToString());
		queryArguments.Add("order", "desc");

		modifiedQueryArguments = queryArguments.ToString();
		
		Log.Verbose($"modifiedQueryArguments: {modifiedQueryArguments}");
		
	
		/*
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
		*/
		
		
		var modifiedEndpointWithQuery = (string.IsNullOrWhiteSpace(modifiedQueryArguments) ? modifiedEndpoint : $"{modifiedEndpoint}?{modifiedQueryArguments}");

		var stopwatch = new Stopwatch();
		stopwatch.Start();
		
		using (var request = new HttpRequestMessage(HttpMethod.Get, modifiedEndpointWithQuery))
		{
			if (useAuth)
			{
				request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _rtClient.AuthResponse?.AccessToken ?? string.Empty);
			}
			else
			{
				request.Headers.Authorization = null;
			}

			//var cacheItem = CacheSQLiteConnection.Table<CacheItem>().SingleOrDefault(x => x.Endpoint.Equals(modifiedEndpointWithQuery, StringComparison.OrdinalIgnoreCase));
			//var cacheItem = CacheSQLiteConnection.Table<CacheItem>().Where(x => x.Endpoint.Equals(modifiedEndpointWithQuery, StringComparison.OrdinalIgnoreCase)).Take(1);
			var cacheItem =_rtClient.CacheSQLiteConnection.Query<CommentCacheItem>("SELECT * FROM comment_cache_item WHERE url = ? LIMIT 1", modifiedEndpointWithQuery).FirstOrDefault();
			
			// Only bother with ETag if cache file exists.
			if (cacheFileExists && string.IsNullOrEmpty(cacheItem?.ETag) == false)
			{
				request.Headers.Add("If-None-Match", cacheItem.ETag);
			}
			
			var response = await _rtClient.HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
			
			stopwatch.Stop();
			
			// Add rate limiting of 1 request every 500ms.
			if (stopwatch.ElapsedMilliseconds < 500)
			{
				await Task.Delay(500 - (int)stopwatch.ElapsedMilliseconds, cancellationToken).ConfigureAwait(false);
			}

			if (response.StatusCode == HttpStatusCode.TooManyRequests)
			{
				Log.Error($"Rate limited with request: {url}, {modifiedEndpointWithQuery}");
				
				var stringBuilder = new StringBuilder();
				stringBuilder.AppendLine($"{guid} GetAPIRequest: response code {((int)response.StatusCode)} - {url}");
				foreach (var header in response.Headers)
				{
					stringBuilder.AppendLine($"{header.Key} - {string.Join(", ", header.Value)}");
				}

				var pageResponse = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
				stringBuilder.AppendLine(pageResponse);
				Log.Error(stringBuilder.ToString());
				
				await Task.Delay(65 * 1000);
			}
			else if (response.StatusCode == HttpStatusCode.NotModified)
			{
				try
				{
					using (var fileStream = File.OpenRead(fullCacheFileName))
					{
						var cachedResponseObject = JsonSerializer.Deserialize<TResponse>(fileStream);
						
						// cacheItem should never be null here as we won't be setting ETag if it is null
						if (cacheItem is not null)
						{
							cacheItem.LastChecked = DateTime.Now;
							_rtClient.CacheSQLiteConnection.Update(cacheItem);
						}

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
				Log.Error($"{guid} GetAPIRequest: response code 404 - {url}");
				return (false, (int)response.StatusCode, default(TResponse));
			}
			else if (response.StatusCode == HttpStatusCode.Unauthorized)
			{
				Log.Error($"{guid} GetAPIRequest: response code 401 - {url}");
				return (false, (int)response.StatusCode, default(TResponse));
			}
			else if (response.StatusCode != HttpStatusCode.OK)
			{
				var stringBuilder = new StringBuilder();
				stringBuilder.AppendLine($"{guid} GetAPIRequest: response code {((int)response.StatusCode)} - {url}");
				foreach (var header in response.Headers)
				{
					stringBuilder.AppendLine($"{header.Key} - {string.Join(", ", header.Value)}");
				}

				var pageResponse = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
				stringBuilder.AppendLine(pageResponse);
				Log.Error(stringBuilder.ToString());
				Debugger.Break();
				return (false, (int)response.StatusCode, default(TResponse));
			}
			
			using (var memoryStream = new MemoryStream())
			{
				using (var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
				{
					await responseStream.CopyToAsync(memoryStream, cancellationToken).ConfigureAwait(false);
				}

				memoryStream.Position = 0;

				try
				{
					lock (_diskIOLock)
					{
						if (Directory.Exists(episidePath) == false)
						{
							Directory.CreateDirectory(episidePath);
						}
					}

					using (var fileStream = File.Create(fullCacheFileName))
					{
						await memoryStream.CopyToAsync(fileStream).ConfigureAwait(false);
					}
					
					if (response.Headers.ETag != null)
					{
						cacheItem = new CommentCacheItem()
						{
							Url = modifiedEndpointWithQuery,
							ETag = response.Headers.ETag.ToString(),
							LastChecked = DateTime.Now,
						};
				
						_rtClient.CacheSQLiteConnection.InsertOrReplace(cacheItem);
					}
				}
				catch (Exception err)
				{
					Log.Error(err, $"Could not save request to disk, {fullCacheFileName}");
				}

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
					Log.Error(err, $"Could not deserialize json for {url}.");
					return (false, (int)response.StatusCode, default(TResponse));
				}
			}
		}
	}
}