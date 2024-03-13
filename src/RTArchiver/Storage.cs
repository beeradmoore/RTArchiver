using Serilog;

namespace RTArchiver;

public class Storage
{
	public static string ArchivePath { get; private set; } = string.Empty;
	public static string CachePath { get; private set; } = string.Empty;
	public static string LogsPath { get; private set; } = string.Empty;
	public static string VideosPath { get; private set; } = string.Empty;


	static Storage()
	{
		var archiveCachePath = Environment.GetEnvironmentVariable("RT_ARCHIVE_PATH");
		if (string.IsNullOrEmpty(archiveCachePath) == true)
		{
			archiveCachePath = "archive";
		}
		ChangeArchivePath(archiveCachePath);
	}

	public static void Init()
	{
		// This is really just dud code, it causes the system to setup the folders we actually use for logging.
		Log.Information("Setting up storage system");
	}

	public static void ChangeArchivePath(string path)
	{
		var archivePath = path;
		var cachePath = Path.Combine(archivePath, "cache");
		var logsPath = Path.Combine(archivePath, "logs");
		var videosPath = Path.Combine(archivePath, "videos");
		try
		{
			if (Directory.Exists(archivePath) == false)
			{
				Log.Information($"Creating archive directory {archivePath}");
				Directory.CreateDirectory(archivePath);
			}
		
			if (Directory.Exists(cachePath) == false)
			{
				Log.Information($"Creating cache directory {cachePath}");
				Directory.CreateDirectory(cachePath);
			}
		
			if (Directory.Exists(logsPath) == false)
			{
				Log.Information($"Creating logs directory {logsPath}");
				Directory.CreateDirectory(logsPath);
			}
		
			if (Directory.Exists(videosPath) == false)
			{
				Log.Information($"Creating videos directory {videosPath}");
				Directory.CreateDirectory(videosPath);
			}
			
			Log.Logger = new LoggerConfiguration()
				.WriteTo.Debug()
				.WriteTo.File(Path.Combine(logsPath, "rt_archiver_.log"), rollingInterval: RollingInterval.Day)
				.CreateLogger();

			ArchivePath = archivePath;
			CachePath = cachePath;
			LogsPath = logsPath;
			VideosPath = videosPath;
			
			Log.Information($"Using archive directory {archivePath}");
			Log.Information($"Using cache directory {cachePath}");
			Log.Information($"Using logs directory {logsPath}");
			Log.Information($"Using videos directory {videosPath}");
		}
		catch (Exception err)
		{
			Log.Error(err, $"Could not create storage folders.");
			Console.WriteLine("ERROR: Could not create storage folders.");
			Environment.Exit(1);
		}
	}
}