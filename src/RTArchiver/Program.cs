using System.Diagnostics;
using System.Text.Json;
using RTArchiver;

var rtClient = new RTClient();

// RTClient loads auth.json, so we may already be logged in.
if (rtClient.IsLoggedIn() == false)
{
	Console.WriteLine("Please to your roosterteeth.com account");

	Console.Write("Username: ");
	var username = Console.ReadLine() ?? string.Empty;

	// Get the password but hide the text.
	Console.Write("Password: ");
	var password = string.Empty;
	while (true)
	{
		var consoleKeyInfo = Console.ReadKey(true);
		if (consoleKeyInfo.Key == ConsoleKey.Enter)
		{
			break;
		}

		if (consoleKeyInfo.Key == ConsoleKey.Backspace)
		{
			if (password.Length > 0)
			{
				password = password.Remove(password.Length - 1);
			}
		}
		else if (char.IsControl(consoleKeyInfo.KeyChar) == false)
		{
			password += consoleKeyInfo.KeyChar;
		}

		// Draw password placeholder.
		Console.SetCursorPosition(0, Console.CursorTop);
		Console.Write(new string(' ', Console.BufferWidth));
		Console.SetCursorPosition(0, Console.CursorTop);
		Console.Write("Password: ");
		Console.Write(string.Empty.PadLeft(password.Length, '*'));
	}

	// Login with the new details.
	var didLogin = await rtClient.Login(username, password);
	if (didLogin == false)
	{
		return;
	}
}

var meResponse = await rtClient.GetMe();
Console.WriteLine($"Welcome {meResponse.Attributes.Username}");

Console.WriteLine("\nLoading genres");
var genres = await rtClient.GetGenres();
if(genres != null)
{
	Console.WriteLine($"Found: {genres.Count}");
	Console.WriteLine(JsonSerializer.Serialize(genres[0], new JsonSerializerOptions { WriteIndented = true }));
}
var seasons = await rtClient.GetSeasons("camp-camp");
if(seasons != null)
{
	Console.WriteLine(JsonSerializer.Serialize(seasons.Data[0], new JsonSerializerOptions { WriteIndented = true}));
}
//var channels = await rtClient.GetChannels();
//var shows = await rtClient.GetShows();

Console.WriteLine("\nLoading channels");
var channels = await rtClient.GetChannels();
Console.WriteLine($"Found: {channels.Count}");

Console.WriteLine("\nLoading shows");
var shows = await rtClient.GetShows();
Console.WriteLine($"Found: {shows.Count}");


Debugger.Break();
//Debugger.Break();