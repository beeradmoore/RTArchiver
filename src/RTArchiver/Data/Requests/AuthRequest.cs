using System.Text.Json.Serialization;

namespace RTArchiver.Data.Requests;

public class AuthRequest
{
	[JsonPropertyName("client_id")]
	public string ClientId => "4338d2b4bdc8db1239360f28e72f0d9ddb1fd01e7a38fbb07b4b1f4ba4564cc5";

	[JsonPropertyName("grant_type")]
	public string GrantType => "password";

	[JsonPropertyName("scope")]
	public string Scope => "user public";

	[JsonPropertyName("username")]
	public string Username { get; set; } = string.Empty;

	[JsonPropertyName("password")]
	public string Password { get; set; } = string.Empty;
}