using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using RTArchiver.Data;

namespace RTArchiver;

public class RTClient
{
	HttpClient _httpClient = new HttpClient();
	AuthResponse? _authResponse = null;

	public static string ArchivePath = "archive";

	public List<Genre> Genres { get; } = new List<Genre>();

	static RTClient()
	{
		var rtArchivePath = Environment.GetEnvironmentVariable("RT_ARCHIVE_PATH");
		if (String.IsNullOrEmpty(rtArchivePath) == false)
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
		return (_authResponse is not null);
	}

	public async Task<bool> Login(string username, string password)
	{
		var authRequest = new AuthRequest()
		{
			Username = username,
			Password = password,
		};
		
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

			if (String.IsNullOrEmpty(authResponse.Error) == false)
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
			Console.WriteLine($"Error: Could not log in.");
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
		var refreshRequest = new RefreshRequest() { RefreshToken = _authResponse?.RefreshToken ?? String.Empty };
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
            if (String.IsNullOrEmpty(authResponse.Error) == false)
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
            Console.WriteLine($"Error: Could not log in.");
            Console.WriteLine(err.Message);
			Logout();
            return false;
        }
    }

	async Task<TResponse?> GetAPIRequest<TResponse>(string url, bool useAuth = true)
	{
		using (var request = new HttpRequestMessage(HttpMethod.Get, url))
		{
			if (useAuth == true)
			{
				request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _authResponse?.AccessToken ?? String.Empty);
			}

			var response = await _httpClient.SendAsync(request);

			// TODO: Check http status. May need to handle auth responses here for refreshing access token.
			
			#if DEBUG
			// Helps debug a specific endpoint as plaintext.
			if (url.Contains("shows"))
			{
				var responseData = await response.Content.ReadAsStringAsync();
				Debugger.Break();
			}
			#endif
		
			return await response.Content.ReadFromJsonAsync<TResponse>();
		}
	}

	public async Task<MeResponse?> GetMe()
	{
		return await GetAPIRequest<MeResponse>("https://business-service.roosterteeth.com/api/v1/me");
	}

	public async Task<GenresResponse?> GetGenres()
	{
		Genres.Clear();
		var genresResponse = await GetAPIRequest<GenresResponse>("https://svod-be.roosterteeth.com/api/v1/genres");

		if (genresResponse is not null)
		{
			Genres.AddRange(genresResponse.Data);
		}

		return genresResponse;
	}
	
	public async Task<ChannelsResponse?> GetChannels()
	{
		var channelsResponse = await GetAPIRequest<ChannelsResponse>("https://svod-be.roosterteeth.com/api/v1/channels", false);
		return channelsResponse;
	}
	
	public async Task<ShowsResponse?> GetShows()
	{
		var showsResponse = await GetAPIRequest<ShowsResponse>("https://svod-be.roosterteeth.com/api/v1/shows");
		return showsResponse;
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