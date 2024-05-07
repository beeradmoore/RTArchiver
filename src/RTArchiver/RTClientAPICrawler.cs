using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using RTArchiver.Data.Responses;
using Serilog;
using ThreadState = System.Threading.ThreadState;

namespace RTArchiver;

public class RTClientAPICrawler
{
	RTClient _rtClient;

	
	static object _dictionaryLock = new object();

	List<Thread> _threads = new List<Thread>();
	
	// Used to track what has already bene cached so we don't need to cache it again.
	readonly Dictionary<string, int> _cachedLinksQueueReference = new Dictionary<string, int>();
	readonly ConcurrentBag<string> _apisToFetch = new ConcurrentBag<string>();

	readonly string _crawlCachePath = Path.Combine(Storage.CachePath, "crawl");

	readonly string _linksFile;
	readonly string _linksHttpFile;
	readonly string _unknownApisFile;
	readonly string _notFoundFile;
	readonly string _forbiddenFile;
	readonly string _unauthorizedFile;
	readonly string _unknownStatusCodeFile;
	
	static object _linksFileLock = new object();
	static object _linksHttpFileLock = new object();
	static object _unknownApisFileLock = new object();
	static object _notFoundFileLock = new object();
	static object _forbiddenFileLock = new object();
	static object _unauthorizedFileLock = new object();
	static object _unknownStatusCodeFileLock = new object();

	
	public RTClientAPICrawler(RTClient rtClient)
	{
		_rtClient = rtClient;
		_crawlCachePath = Path.Combine(Storage.CachePath, "crawl");
		if (Directory.Exists(_crawlCachePath) == false)
		{
			Directory.CreateDirectory(_crawlCachePath);
		}
		
		_linksFile = Path.Combine(_crawlCachePath, "valid_links.txt");
		_linksHttpFile = Path.Combine(_crawlCachePath, "links_http.txt");
		_unknownApisFile = Path.Combine(_crawlCachePath, "unknown_apis.txt");
		_notFoundFile = Path.Combine(_crawlCachePath, "status_not_found.txt");
		_forbiddenFile = Path.Combine(_crawlCachePath, "status_forbidden.txt");
		_unauthorizedFile = Path.Combine(_crawlCachePath, "status_unauthorized.txt");
		_unknownStatusCodeFile = Path.Combine(_crawlCachePath, "status_unknown.txt");
	}
	

	public void StartAndWait()
	{
		Log.Information($"Starting: RTClientAPICrawler with {_rtClient.NumberOfThreads} threads.");
		
		
		// TODO: Backup instead of delete
		if (File.Exists(_linksFile))
		{
			File.Delete(_linksFile);
		}

		if (File.Exists(_linksHttpFile))
		{
			File.Delete(_linksHttpFile);
		}
		
		if (File.Exists(_unknownApisFile))
		{
			File.Delete(_unknownApisFile);
		}

		if (File.Exists(_notFoundFile))
		{
			File.Delete(_notFoundFile);
		}
		
		if (File.Exists(_forbiddenFile))
		{
			File.Delete(_forbiddenFile);
		}

		if (File.Exists(_unauthorizedFile))
		{
			File.Delete(_unauthorizedFile);
		}
		
		if (File.Exists(_unknownStatusCodeFile))
		{
			File.Delete(_unknownStatusCodeFile);
		}
		
		// Start with a few links and then we add more over time.
		
		_apisToFetch.Add("/api/v1/products");
		_apisToFetch.Add("/api/v1/campaigns");
		_apisToFetch.Add("/api/v1/genres");
		_apisToFetch.Add("/api/v1/channels");
		_apisToFetch.Add("/api/v1/livestreams");
		_apisToFetch.Add("/api/v1/playlists");
		_apisToFetch.Add("/api/v1/shows");
		_apisToFetch.Add("/api/v1/episodes");
		_apisToFetch.Add("/api/v1/watch");
		
		foreach (var apiToFetch in _apisToFetch)
		{
			File.AppendAllText(_linksFile, $"{apiToFetch}\n");
		}

		var tasks = new List<Task>();
		var cancellationTokenSource = new CancellationTokenSource();
		for (var threadNumber = 0; threadNumber < _rtClient.NumberOfThreads; ++threadNumber)
		{
			tasks.Add(APICrawlerWorker(threadNumber, cancellationTokenSource.Token));
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

		//await Task.WhenAll(tasks);
	}
	

	async Task APICrawlerWorker(int threadNumber, CancellationToken cancellationToken)
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

				Log.Information($"_apisToFetch.Count: {_apisToFetch.Count}");

				var didLoad = false;

				// Try to get an item from the _apisToFetch list, if we can't sleep until we failed to get something 20 times in a row.
				if (_apisToFetch.TryTake(out string? linkToCache) == true)
				{
					try
					{
						await ProcessEndpointAsync(linkToCache, cancellationToken).ConfigureAwait(false);
						failedToFetchCount = 0;
						didLoad = true;
					}
					catch (Exception err)
					{
						++failedToFetchCount;
						Log.Error(err, $"Failed to fetch {linkToCache}, adding it back into the list.");
						// Didn't fetch this so we add it back in.
						// There is potential for an endless loop here though...
						_apisToFetch.Add(linkToCache);
						Debugger.Break();
					}
				}
				else
				{
					++failedToFetchCount;
				}

				if (didLoad == false)
				{
					if (_apisToFetch.Count == 0)
					{
						// This means we have failed to have any items added to the list for 10 seconds, so we are likely done.
						if (failedToFetchCount > 5 && _apisToFetch.Count == 0)
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
	
	
	async Task ProcessEndpointAsync(string linkToCache, CancellationToken cancellationToken)
	{
		Log.Information($"Processing {linkToCache}");
		var response = await _rtClient.GetPaginatedAPIRequest<GenericLink, GenericLinksResponse>(linkToCache, cancellationToken).ConfigureAwait(false);
		if (response.Success)
		{
			lock (_linksFileLock)
			{
				File.AppendAllText(_linksFile, $"{linkToCache}\n");
			}
			
			foreach (var genericLink in response.Items)
			{
				if (genericLink.Links is not null)
				{
					foreach (var genericLinkDictionary in genericLink.Links)
					{
						ProcessLinkDictionary(genericLinkDictionary);
					}
				}

				// Do the same checks for canonical links
				if (genericLink.CanonicalLinks is not null)
				{
					foreach (var genericLinkDictionary in genericLink.CanonicalLinks)
					{
						ProcessLinkDictionary(genericLinkDictionary);
					}
				}
			}
		}
		else
		{
			if (response.LastStatusCode == 401)
			{
				lock (_unauthorizedFileLock)
				{
					File.AppendAllText(_unauthorizedFile, $"{linkToCache}\n");
				}
			}
			else if (response.LastStatusCode == 403)
			{
				lock (_forbiddenFileLock)
				{
					File.AppendAllText(_forbiddenFile, $"{linkToCache}\n");
				}
			}
			else if (response.LastStatusCode == 404)
			{
				lock (_notFoundFileLock)
				{
					File.AppendAllText(_notFoundFile, $"{linkToCache}\n");
				}
			}
			else
			{
				lock (_unknownStatusCodeFileLock)
				{
					File.AppendAllText(_unknownStatusCodeFile, $"{response.LastStatusCode} - {linkToCache}\n");
				}
			}
			
			// If we failed we stop.
			Log.Error($"Invalid status code {response.LastStatusCode} for endpoint {linkToCache}");
		}
	}
	

	void ProcessLinkDictionary(KeyValuePair<string, object?> genericLinkDictionary)
	{
		// Sometimes this value is null.
		if (genericLinkDictionary.Value is null)
		{
			return;
		}

		if (genericLinkDictionary.Value is JsonElement jsonElement)
		{
			// Sometimes this value is a string
			if (jsonElement.ValueKind == JsonValueKind.String)
			{
				var linkUrl = jsonElement.GetString() ?? string.Empty;
				ParseGenericLinkString(linkUrl);
				return;
			}

			// And other times it's a dictionary
			if (jsonElement.ValueKind == JsonValueKind.Object)
			{
				var dictionary = jsonElement.Deserialize<Dictionary<string, string>>();
				if (dictionary is not null)
				{
					foreach (var subDictionaryItem in dictionary)
					{
						var linkUrl = subDictionaryItem.Value ?? string.Empty;
						ParseGenericLinkString(linkUrl);
					}
					return;
				}
			}

			Debugger.Break();
		}
		else
		{
			Debugger.Break();
		}
	}
	
	
	void ParseGenericLinkString(string str)
	{
		// Skip when there are no links.
		if (string.IsNullOrEmpty(str) == true)
		{
			return;
		}

		// Skip RT-TV link
		if (str == "/live/rt-tv")
		{
			return;
		}

		// Skip any and all http/https links.
		if (str.StartsWith("http", StringComparison.OrdinalIgnoreCase))
		{
			lock (_linksHttpFileLock)
			{
				File.AppendAllText(_linksHttpFile, $"{str}\n");
			}
			return;
		}

		// /series/ links are for webpage, not API

		// Check if the link is something like /watch/something
		if (str.StartsWith("/api/v1/", StringComparison.OrdinalIgnoreCase) == false)
		{
			if (str.StartsWith("/bonus_features/", StringComparison.OrdinalIgnoreCase) ||
			    str.StartsWith("/channels/", StringComparison.OrdinalIgnoreCase) ||
			    str.StartsWith("/episodes/", StringComparison.OrdinalIgnoreCase) ||
			    str.StartsWith("/livestreams/", StringComparison.OrdinalIgnoreCase) ||
			    str.StartsWith("/seasons/", StringComparison.OrdinalIgnoreCase) ||
			    str.StartsWith("/shows/", StringComparison.OrdinalIgnoreCase) ||
			    str.StartsWith("/videos/", StringComparison.OrdinalIgnoreCase) ||
			    str.StartsWith("/watch/", StringComparison.OrdinalIgnoreCase))
			{
				// Fix formatting
				str = $"/api/v1{str}";
			}
			else
			{
				lock (_unknownApisFileLock)
				{
					File.AppendAllText(_unknownApisFile, $"{str}\n");
				}
				return;
			}
		}
		
		
		// If the value could not be added 
		lock (_dictionaryLock)
		{
			if (_cachedLinksQueueReference.ContainsKey(str) == true)
			{
				// Increment this number, for statistical fun.
				++_cachedLinksQueueReference[str];
				return;
			}
			else
			{
				// Add it to the queue so we don't add it to _apisToFetch again
				_cachedLinksQueueReference[str] = 1;
			}
		}

		_apisToFetch.Add(str);
	}

	
	void CrawlComments()
	{
		/*
		Format is:
		https://comments.roosterteeth.com/api/v2/topics/{episode_uuid}/comments?per_page=20&page=1&sort=-created_at

		Can get total commments count with (this include child comments)
		https://comments.roosterteeth.com/api/v2/topics/{episode_uuid}/comments/count

		A comment looks like,
	   {
	       "id": 1540007,
	       "uuid": "37038025-40c4-4282-9cec-13f2a0b37bc3",
	       "topic_uuid": "003c9926-464e-11e7-a302-065410f210c4",
	       "parent_uuid": null,
	       "owner_uuid": "3ca4adcb-c7f7-4d5b-b6f9-a46e081842fd",
	       "message": "I feel like people don't talk about how much of a mood Gwen and Max are.",
	       "created_at": "2020-08-15T21:43:12.000Z",
	       "updated_at": "2020-08-15T21:43:12.000Z",
	       "edited_at": null,
	       "topic_type": "episode",
	       "spoiler": false,
	       "likes_count": 1,
	       "owner_roles": [],
	       "owner_badges": [],
	       "owner_name": "YeetusDeletusComingForYourFetus",
	       "owner_status": "free",
	       "owner_image": "https://cdn.roosterteeth.com/image/upload/f_auto/comm/3ca4adcb-c7f7-4d5b-b6f9-a46e081842fd/8ebf861cb4ad5c9dcd24a2e8552d249bf4311e0578c9cf7912daa8f8545b94bb.jpg",
	       "child_comments_total_count": 1,
	       "staff_member_has_replied": false,
	       "created_by_staff": false,
	       "type": "comment",
	       "liked_status": false
	   },

	   But then we can load comments in a thread like,
	   https://comments.roosterteeth.com/api/v2/comments/{comment_uuid}/thread?per_page=1000
	   
	   which then looks like,
	
       {
         "id": 1709814,
         "uuid": "9484a689-e2d2-4f05-ada9-46dc4fd0c235",
         "topic_uuid": "003c9926-464e-11e7-a302-065410f210c4",
         "parent_uuid": "37038025-40c4-4282-9cec-13f2a0b37bc3",
         "owner_uuid": "ad5c26e7-afbd-4cf5-a49e-c8965507d81d",
         "message": "@YeetusDeletusComingForYourFetus lmaooo my favorite characters are most of the adults Gwen is my favorite out of all of them",
         "created_at": "2020-11-30T17:40:05.000Z",
         "updated_at": "2020-11-30T17:40:05.000Z",
         "edited_at": null,
         "topic_type": "episode",
         "spoiler": false,
         "likes_count": 0,
         "owner_roles": [],
         "owner_badges": [],
         "owner_name": "ImCrying",
         "owner_status": "free",
         "owner_image": "https://cdn.roosterteeth.com/image/upload/f_auto/comm/ad5c26e7-afbd-4cf5-a49e-c8965507d81d/2cfd79bf107c5cca25a9b8630156b44ac89a3f294d9fdac41201c3ef403178b1.jpeg",
         "child_comments_total_count": 0,
         "staff_member_has_replied": false,
         "created_by_staff": false,
         "type": "comment",
         "liked_status": false
       }
	   */
	}
}
