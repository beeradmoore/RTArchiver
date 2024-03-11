﻿using System.Diagnostics;
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

Console.WriteLine("\nLoading channels");
var channels = await rtClient.GetChannels();
Console.WriteLine($"Found: {channels.Count}");
if (channels.Count > 0)
{
	Console.WriteLine(JsonSerializer.Serialize(channels[0], new JsonSerializerOptions { WriteIndented = true }));
}

Console.WriteLine("\nLoading genres");
var genres = await rtClient.GetGenres();
Console.WriteLine($"Found: {genres.Count}");
if(genres.Count > 0)
{
	Console.WriteLine(JsonSerializer.Serialize(genres[0], new JsonSerializerOptions { WriteIndented = true }));
}
var slug = "camp-camp";
Console.WriteLine($"\nLoading seasons for {slug}");
var seasons = await rtClient.GetSeasons(slug);
Console.WriteLine($"Found: {seasons.Count}");
if (seasons.Count > 0)
{
	Console.WriteLine(JsonSerializer.Serialize(seasons[0], new JsonSerializerOptions { WriteIndented = true}));
}
//var channels = await rtClient.GetChannels();
//var shows = await rtClient.GetShows();

Console.WriteLine("\nLoading shows");
var shows = await rtClient.GetShows();
Console.WriteLine($"Found: {shows.Count}");
if(shows.Count > 0)
{
	Console.WriteLine(JsonSerializer.Serialize(shows[0], new JsonSerializerOptions { WriteIndented = true }));
}


Debugger.Break();
//Debugger.Break();