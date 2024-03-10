using System.Diagnostics;
using RTArchiver;

var rtClient = new RTClient();

// RTClient loads auth.json, so we may already be logged in.
if (rtClient.IsLoggedIn() == false)
{
	Console.WriteLine("Please to your roosterteeth.com account");
	
	Console.Write("Username: ");
	var username = Console.ReadLine() ?? String.Empty;
	
	// Get the password but hide the text.
	Console.Write("Password: ");
	var password = String.Empty;
	while (true)
	{
		var consoleKeyInfo = Console.ReadKey(true);
		if (consoleKeyInfo.Key == ConsoleKey.Enter)
		{
			break;
		}
		else if (consoleKeyInfo.Key == ConsoleKey.Backspace)
		{
			if (password.Length > 0)
			{
				password = password.Remove(password.Length - 1);
			}
		}
		else if (char.IsControl(consoleKeyInfo.KeyChar) == false)
		{
			password += (char)consoleKeyInfo.KeyChar;
		}
		
		// Draw password placeholder.
		Console.SetCursorPosition(0, Console.CursorTop);
		Console.Write(new String(' ', Console.BufferWidth));
		Console.SetCursorPosition(0, Console.CursorTop);
		Console.Write("Password: ");
		Console.Write(String.Empty.PadLeft(password.Length, '*'));
	}
	
	// Login with the new details.
	var didLogin = await rtClient.Login(username, password);
	if (didLogin == false)
	{
		return;
	}
}
