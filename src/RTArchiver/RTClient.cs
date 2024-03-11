using System.Net.Http.Headers;
using System.Net.Http.Json;
using RTArchiver.Data;
using RTArchiver.Data.Requests;
using RTArchiver.Data.Responses;

namespace RTArchiver;

public class RTClient
{
	public static string ArchivePath = "archive";
	readonly HttpClient _httpClient = new HttpClient();
	AuthResponse? _authResponse;
	
	public Dictionary<string, Genre> Genres { get; } = new Dictionary<string, Genre>();
	public Dictionary<string, Show> Shows { get; } = new Dictionary<string, Show>();
	public Dictionary<string, Channel> Channels { get; } = new Dictionary<string, Channel>();

	static RTClient()
	{
		var rtArchivePath = Environment.GetEnvironmentVariable("RT_ARCHIVE_PATH");
		if (string.IsNullOrEmpty(rtArchivePath) == false)
		{
			ArchivePath = rtArchivePath;
		}

		try
		{
			if (Directory.Exists(ArchivePath) == false)
			{
				Directory.CreateDirectory(ArchivePath);
			}
		}
		catch (Exception err)
		{
			Console.WriteLine($"Error: Could not create ArchivePath {ArchivePath}");
			Console.WriteLine(err.Message);
			Environment.Exit(1);
		}
	}

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
				Console.WriteLine("Error: Could not get a valid response from the server.");
				Logout();
				return false;
			}

			if (string.IsNullOrEmpty(authResponse.Error) == false)
			{
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
				Console.WriteLine("Error: Could not get a valid response from the server.");
				Logout();
				return false;
			}

			if (string.IsNullOrEmpty(authResponse.Error) == false)
			{
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
			Console.WriteLine("Error: Could not log in.");
			Console.WriteLine(err.Message);
			Logout();
			return false;
		}
	}

	async Task<TResponse?> GetAPIRequest<TResponse>(string url, int page = 1, int perPage = 500, string order = "asc", bool useAuth = true)
	{
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
		}

		//Console.WriteLine(url);

		using (var request = new HttpRequestMessage(HttpMethod.Get, url))
		{
			if (useAuth)
			{
				request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _authResponse?.AccessToken ?? string.Empty);
			}

			var response = await _httpClient.SendAsync(request);

			// TODO: Check http status. May need to handle auth responses here for refreshing access token.

#if DEBUG
			// Helps debug a specific endpoint as plaintext.
			if (url.Contains("shows", StringComparison.InvariantCultureIgnoreCase))
			{
				var responseData = await response.Content.ReadAsStringAsync();
				//Debugger.Break();
			}
#endif

			return await response.Content.ReadFromJsonAsync<TResponse>();
		}
	}

	public async Task<MeResponse?> GetMe()
	{
		return await GetAPIRequest<MeResponse>("https://business-service.roosterteeth.com/api/v1/me");
	}

	public async Task<List<Genre>> GetGenres()
	{
		List<Genre> genres = new List<Genre>();
		
		Genres.Clear();
		var genresResponse = await GetAPIRequest<GenresResponse>("https://svod-be.roosterteeth.com/api/v1/genres");

		
		if (genresResponse is not null)
		{
			foreach (var genre in genresResponse.Data)
			{
				genres.Add(genre);
				if (Genres.ContainsKey(genre.Slug))
				{
					Console.WriteLine($"Error: Duplicate genre key found, {genre.Slug}");
				}

				Genres[genre.Slug] = genre;
			}
		}

		return genres;
	}
	
	public async Task<List<Channel>> GetChannels()
	{
		var channels  = new List<Channel>();

		var page = 1;
		ChannelsResponse channelsResponse;
		do
		{
			// TODO: Add attempts and if a request fails and returns null try attempt again.
			channelsResponse = await GetAPIRequest<ChannelsResponse>("https://svod-be.roosterteeth.com/api/v1/channels", page, 100, useAuth: false);

			//Console.WriteLine($"Loaded page {page}, page: {channelsResponse.Page}, perPage: {channelsResponse.PerPage}, totalPages: {channelsResponse.TotalPages}, totalResults: {channelsResponse.TotalResults}");
			if (channelsResponse != null)
			{
				channels.AddRange(channelsResponse.Data);
				++page;
			}

			// TotalPages appears to change over time, but this should still work ok. 
		} while (page < channelsResponse.TotalPages);

		// Cache on this object.
		Channels.Clear();
		foreach (var channel in channels)
		{
			if (Channels.ContainsKey(channel.Slug))
			{
				Console.WriteLine($"Error: Duplicate channel key found, {channel.Slug}");
			}

			Channels[channel.Slug] = channel;
		}
		
		return channels;
	}

	public async Task<List<Show>> GetShows()
	{
		var shows = new List<Show>();

		var page = 1;
		ShowsResponse showsResponse;
		do
		{
			// TODO: Add attempts and if a request fails and returns null try attempt again.
			showsResponse = await GetAPIRequest<ShowsResponse>("https://svod-be.roosterteeth.com/api/v1/shows", page, 100);

			//Console.WriteLine($"Loaded page {page}, page: {showsResponse.Page}, perPage: {showsResponse.PerPage}, totalPages: {showsResponse.TotalPages}, totalResults: {showsResponse.TotalResults}");
			if (showsResponse != null)
			{
				shows.AddRange(showsResponse.Data);
				++page;
			}

			// TotalPages appears to change over time, but this should still work ok. 
		} while (page < showsResponse.TotalPages);

		// Cache on this object.
		Shows.Clear();
		foreach (var show in shows)
		{
			if (Shows.ContainsKey(show.Slug))
			{
				Console.WriteLine($"Error: Duplicate show key found, {show.Slug}");
			}

			Shows[show.Slug] = show;
		}
		
		return shows;
	}

	public async Task<List<Season>> GetSeasons(string slug)
	{
		var seasonsResponse = await GetAPIRequest<SeasonsResponse>($"https://svod-be.roosterteeth.com/api/v1/shows/{slug}/seasons");
		return seasonsResponse?.Data ?? new List<Season>();
	}
	
	// TODO: Handle these APIs, set useAuth when its not required 
	// https://svod-be.roosterteeth.com/api/v1/channels (noauth)
	// https://svod-be.roosterteeth.com/api/v1/shows?per_page=50&order=desc&page=1

	// Other samples
	// https://svod-be.roosterteeth.com/api/v1/shows/camp-camp
	// Some of these have bonus features in it as well, we should expose those.
	// https://roosterteeth.com/episodes?channel_id=red-vs-blue-universe
	// https://roosterteeth.com/watch/red-vs-blue-season-4-episode-58
}