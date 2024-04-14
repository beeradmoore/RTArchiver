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

	static object _fileWriteLock = new object();
	static object _dictionaryLock = new object();

	bool _cacheGoBrrrrRunning = false;
	List<Thread> _threads = new List<Thread>();
	
	// Used to track what has already bene cached so we don't need to cache it again.
	Dictionary<string, int> _cachedLinksQueueReference = new Dictionary<string, int>();
	ConcurrentBag<string> _apisToFetch = new ConcurrentBag<string>();
	
	public RTClientAPICrawler(RTClient rtClient)
	{
		_rtClient = rtClient;
	}
	

	public async Task StartAsync(int threadsToStart = 0)
	{
		// Override default value to current number of threads.
		if (threadsToStart < 1)
		{
			threadsToStart = Environment.ProcessorCount;
		}
		
		Log.Information($"Starting: RTClientAPICrawler, {threadsToStart} threads.");
		
		_cacheGoBrrrrRunning = true;
		lock (_fileWriteLock)
		{
			if (File.Exists("links.txt"))
			{
				File.Delete("links.txt");
			}

			if (File.Exists("links_http.txt"))
			{
				File.Delete("links_http.txt");
			}
			
			if (File.Exists("unknown_apis.txt"))
			{
				File.Delete("unknown_apis.txt");
			}
		}

		// Start with a single link, and then we will find more over time.
		
		_apisToFetch.Add("/api/v1/products");
		_apisToFetch.Add("/api/v1/campaigns");
		_apisToFetch.Add("/api/v1/genres");
		//_apisToFetch.Add("/api/v1/bonus_features");
		_apisToFetch.Add("/api/v1/channels");
		_apisToFetch.Add("/api/v1/episodes");
		_apisToFetch.Add("/api/v1/livestreams");
		//_apisToFetch.Add("/api/v1/seasons");
		//_apisToFetch.Add("/api/v1/series");
		_apisToFetch.Add("/api/v1/playlists");
		_apisToFetch.Add("/api/v1/shows");
		//_apisToFetch.Add("/api/v1/videos");
		_apisToFetch.Add("/api/v1/watch");
		//_apisToFetch.Add("/api/v1/series/infinights");
			
		lock (_fileWriteLock)
		{
			//File.AppendAllText("links.txt", "/api/v1/channels\n");
		}

		//threadsToStart = 1;
		var tasks = new List<Task>();
		for (var threadNumber = 0; threadNumber < threadsToStart; ++threadNumber)
		{
			tasks.Add(CacheGoBrrrr_Thread(threadNumber));
		}

		await Task.WhenAll(tasks);

		/*
		threadsToStart = 1;
		// Start up all the 
		for (var threadNumber = 0; threadNumber < threadsToStart; ++threadNumber)
		{
			var thread = new Thread(() => CacheGoBrrrr_Thread(threadNumber));
			_threads.Add(thread);
			thread.Start();
		}
		
		foreach (var thread in _threads)
		{
			thread.Join();
			Debugger.Break();
		}
		*/

		/*
		bool isStillProcessing = true;
		do
		{
			var allThreadsDead = true;
			foreach (var thread in _threads)
			{
				Console.WriteLine(thread.ThreadState);
				if (thread.ThreadState == ThreadState.Running)
				{
					allThreadsDead = false;
					await Task.Delay(TimeSpan.FromSeconds(15));
					break;
				}
			}

			isStillProcessing = (allThreadsDead == false);
			if (isStillProcessing == false)
			{
				Debugger.Break();
			}
		} while (isStillProcessing);
		*/

		Debugger.Break();
		_cacheGoBrrrrRunning = false;
	}
	

	async Task CacheGoBrrrr_Thread(int threadNumber)
	{
		Console.WriteLine($"Starting thread: {threadNumber}");
		try
		{
			// Used to create a random sleep duration.
			var random = new Random();
			var failedToFetchCount = 0;
			do
			{
				Console.WriteLine($"_apisToFetch.Count: {_apisToFetch.Count}");

				var didLoad = false;

				// Try to get an item from the _apisToFetch list, if we can't sleep until we failed to get something 20 times in a row.
				if (_apisToFetch.TryTake(out string? linkToCache) == true)
				{
					try
					{
						await ProcessEndpointAsync(linkToCache).ConfigureAwait(false);
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
					if (failedToFetchCount > 20)
					{
						Log.Information($"Thread has failed to find any item {failedToFetchCount} times. Aborting.");
						return;
					}

					// Wait 1-9.999 seconds and try again
					//Thread.Sleep(random.Next(1000, 9999));
					await Task.Delay(random.Next(1000, 9999)).ConfigureAwait(false);
				}

			} while (true);

			Debugger.Break();

		}
		catch (Exception err2)
		{
			Console.WriteLine($"Error in thread: {threadNumber}");

			Debugger.Break();
		}
		finally
		{
			Console.WriteLine($"Finally thread: {threadNumber}");

		}
		
		Console.WriteLine($"Ending thread: {threadNumber}");

		Debugger.Break();
	}

	
	async Task ProcessEndpointAsync(string linkToCache)
	{
		Log.Information($"Processing {linkToCache}");
		var response = await _rtClient.GetPaginatedAPIRequest<GenericLink, GenericLinksResponse>(linkToCache).ConfigureAwait(false);
		if (response.Success)
		{
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
			// If we failed we stop.
			Console.WriteLine($"Error: Could not make the cache go brr- {linkToCache}");
			Log.Error($"Could not make the cache go brr - {linkToCache}");
			return;
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

			// And other times its a dictionary
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

		// Skip any and all http/https links.
		if (str.StartsWith("http", StringComparison.OrdinalIgnoreCase))
		{
			lock (_fileWriteLock)
			{
				File.AppendAllText("links_http.txt", $"{str}\n");
			}
			return;
		}


		// Check if the link is something like /watch/something
		if (str.StartsWith("/api/v1/", StringComparison.OrdinalIgnoreCase) == false)
		{
			if (str.StartsWith("/bonus_features/", StringComparison.OrdinalIgnoreCase) ||
			    str.StartsWith("/channels/", StringComparison.OrdinalIgnoreCase) ||
			    str.StartsWith("/episodes/", StringComparison.OrdinalIgnoreCase) ||
			    str.StartsWith("/livestreams/", StringComparison.OrdinalIgnoreCase) ||
			    str.StartsWith("/seasons/", StringComparison.OrdinalIgnoreCase) ||
			    str.StartsWith("/series/", StringComparison.OrdinalIgnoreCase) ||
			    str.StartsWith("/shows/", StringComparison.OrdinalIgnoreCase) ||
			    str.StartsWith("/videos/", StringComparison.OrdinalIgnoreCase) ||
			    str.StartsWith("/watch/", StringComparison.OrdinalIgnoreCase))
			{
				// Fix formatting
				str = $"/api/v1{str}";
			}
			else
			{
				lock (_fileWriteLock)
				{
					File.AppendAllText("unknown_apis.txt", $"{str}\n");
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

		
		lock (_fileWriteLock)
		{
			File.AppendAllText("links.txt", $"{str}\n");
		}
	}
}
