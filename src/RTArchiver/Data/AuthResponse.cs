using System.Text.Json;
using System.Text.Json.Serialization;

namespace RTArchiver.Data;

public class AuthResponse
{
	[JsonPropertyName("access_token")]
	public string AccessToken { get; set; } = String.Empty;

	[JsonPropertyName("token_type")]
	public string TokenType { get; set; } = String.Empty;

	[JsonPropertyName("expires_in")]
	public long ExpiresIn { get; set; } = -1L;

	[JsonPropertyName("refresh_token")]
	public string RefreshToken { get; set; } = String.Empty;

	[JsonPropertyName("scope")]
	public string Scope { get; set; } = String.Empty;
	
	[JsonPropertyName("created_at")]
	public long CreatedAt { get; set; } = -1L;

	[JsonPropertyName("user_id")]
	public long UserId { get; set; } = -1L;

	[JsonPropertyName("uuid")]
	public string Uuid { get; set; } = String.Empty;

	[JsonPropertyName("error")]
	public string Error { get; set; } = String.Empty;

	[JsonPropertyName("error_description")]
	public string ErrorDescription { get; set; } = String.Empty;

	[JsonPropertyName("extra_info")]
	public string ExtraInfo { get; set; } = String.Empty;


	public void Save()
	{
		if (String.IsNullOrEmpty(AccessToken) == true || String.IsNullOrEmpty(RefreshToken) == true)
		{
			Console.WriteLine("Error: Could not save AuthResponse, no AccessToken or RefreshToken found.");
			return;
		}

		try
		{
			using (var fileStream = File.Create("auth.json"))
			{
				JsonSerializer.Serialize(fileStream, this, new JsonSerializerOptions() { WriteIndented = true } );
			}
		}
		catch (Exception err)
		{
			Console.WriteLine("Error: Unable to save AuthResponse.");
			Console.WriteLine(err.Message);
		}
	}

	public static AuthResponse? Load()
	{
		if (File.Exists("auth.json") == false)
		{
			return null;
		}
		
		try
		{
			using (var fileStream = File.OpenRead("auth.json"))
			{
				var authRepsonse = JsonSerializer.Deserialize<AuthResponse>(fileStream);
				return authRepsonse;
			}
		}
		catch (Exception err)
		{
			Console.WriteLine("Error: Unable deserialize AuthResponse.");
			Console.WriteLine(err.Message);
			return null;
		}
	}
	
	
	
}