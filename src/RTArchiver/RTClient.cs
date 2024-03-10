using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using RTArchiver.Data;

namespace RTArchiver;

public class RTClient
{
	HttpClient _httpClient = new HttpClient();
	AuthResponse? _authResponse = null;
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
				return false;
			}

			if (String.IsNullOrEmpty(authResponse.Error) == false)
			{
				Console.WriteLine($"Error: Could not log in. ({authResponse.Error})");
				Console.WriteLine(authResponse.ErrorDescription);
				Console.WriteLine(authResponse.ExtraInfo);
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
			return false;
		}
	}

	async Task<MeResponse?> GetAPIRequest<TResponse>(string url)
	{
		using (var request = new HttpRequestMessage(HttpMethod.Get, url))
		{
			request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _authResponse?.AccessToken ?? String.Empty);
			var response = await _httpClient.SendAsync(request);
			// TODO: Check http status. May need to handle auth responses here for refreshing access token.
			var responseData = await response.Content.ReadAsStringAsync();
			return await response.Content.ReadFromJsonAsync<MeResponse>();
		}
	}

	public async Task<MeResponse?> GetMe()
	{
		return await GetAPIRequest<MeResponse>("https://business-service.roosterteeth.com/api/v1/me");
	}
}