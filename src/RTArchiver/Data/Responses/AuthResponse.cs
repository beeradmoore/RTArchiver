using System.Text.Json;
using System.Text.Json.Serialization;

namespace RTArchiver.Data.Responses;

public class AuthResponse
{
	[JsonPropertyName("access_token")]
	public string AccessToken { get; set; } = string.Empty;

	[JsonPropertyName("token_type")]
	public string TokenType { get; set; } = string.Empty;

	[JsonPropertyName("expires_in")]
	public long ExpiresIn { get; set; } = -1L;

	[JsonPropertyName("refresh_token")]
	public string RefreshToken { get; set; } = string.Empty;

	[JsonPropertyName("scope")]
	public string Scope { get; set; } = string.Empty;

	[JsonPropertyName("created_at")]
	public long CreatedAt { get; set; } = -1L;

	[JsonPropertyName("user_id")]
	public long UserId { get; set; } = -1L;

	[JsonPropertyName("uuid")]
	public string Uuid { get; set; } = string.Empty;

	[JsonPropertyName("error")]
	public string Error { get; set; } = string.Empty;

	[JsonPropertyName("error_description")]
	public string ErrorDescription { get; set; } = string.Empty;

	[JsonPropertyName("extra_info")]
	public string ExtraInfo { get; set; } = string.Empty;

	public static string AuthFile()
	{
		return Path.Combine(RTClient.ArchivePath, "auth.json");
	}

	public void Save()
	{
		if (string.IsNullOrEmpty(AccessToken) || string.IsNullOrEmpty(RefreshToken))
		{
			Console.WriteLine("Error: Could not save AuthResponse, no AccessToken or RefreshToken found.");
			return;
		}

		try
		{
			using (var fileStream = File.Create(AuthFile()))
			{
				JsonSerializer.Serialize(fileStream, this, new JsonSerializerOptions { WriteIndented = true });
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
		var authFile = AuthFile();

		if (File.Exists(authFile) == false)
		{
			return null;
		}

		try
		{
			using (var fileStream = File.OpenRead(authFile))
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

	public static void Delete()
	{
		var authFile = AuthFile();

		if (File.Exists(authFile))
		{
			File.Delete(authFile);
		}
	}
}