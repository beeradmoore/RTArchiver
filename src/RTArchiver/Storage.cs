using RTArchiver.Data;
using Serilog;

namespace RTArchiver;

public class Storage
{
	public static string ArchivePath { get; private set; } = string.Empty;
	public static string CachePath { get; private set; } = string.Empty;
	public static string LogsPath { get; private set; } = string.Empty;
	public static string VideosPath { get; private set; } = string.Empty;
	public static string DatabasePath { get; private set; } = string.Empty;
	public static string TempPath { get; private set; } = string.Empty;
	public static string SitemapPath { get; private set; } = string.Empty;
	public static string ImagesPath { get; private set; } = string.Empty;

	public static string UsersPath { get; private set; } = string.Empty;

	public static string CommentsPath { get; private set; } = string.Empty;

	public static string BadgesPath { get; private set; } = string.Empty;

	public static void Init(string archiveCachePath)
	{
		// This is really just dud code, it causes the system to setup the folders we actually use for logging.
		Log.Information("Setting up storage system");
		ArchivePath = archiveCachePath;
		ChangeArchivePath(archiveCachePath);
	}

	public static void ChangeArchivePath(string path)
	{
		var archivePath = path;
		var cachePath = Path.Combine(archivePath, "cache");
		var logsPath = Path.Combine(archivePath, "logs");
		var videosPath = Path.Combine(archivePath, "videos");
		var databasePath = Path.Combine(archivePath, "database");
		var tempPath = Path.Combine(archivePath, "temp");
		var sitemapPath = Path.Combine(archivePath, "sitemap");
		var imagesPath = Path.Combine(archivePath, "images");
		var usersPath = Path.Combine(archivePath, "users");
		var commentsPath = Path.Combine(archivePath, "comments");
		var badgesPath = Path.Combine(archivePath, "badges");
		
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
		
			if (Directory.Exists(databasePath) == false)
			{
				Log.Information($"Creating database directory {databasePath}");
				Directory.CreateDirectory(databasePath);
			}
		
			if (Directory.Exists(tempPath) == false)
			{
				Log.Information($"Creating temp directory {tempPath}");
				Directory.CreateDirectory(tempPath);
			}
		
			if (Directory.Exists(sitemapPath) == false)
			{
				Log.Information($"Creating sitemap directory {sitemapPath}");
				Directory.CreateDirectory(sitemapPath);
			}
		
			if (Directory.Exists(imagesPath) == false)
			{
				Log.Information($"Creating images directory {imagesPath}");
				Directory.CreateDirectory(imagesPath);
			}
		
			if (Directory.Exists(usersPath) == false)
			{
				Log.Information($"Creating users directory {usersPath}");
				Directory.CreateDirectory(usersPath);
			}
		
			if (Directory.Exists(commentsPath) == false)
			{
				Log.Information($"Creating comments directory {commentsPath}");
				Directory.CreateDirectory(commentsPath);
			}
		
			if (Directory.Exists(badgesPath) == false)
			{
				Log.Information($"Creating badges directory {badgesPath}");
				Directory.CreateDirectory(badgesPath);
			}
			
			Log.Logger = new LoggerConfiguration()
				.WriteTo.Console(outputTemplate: "{Message:ij}{NewLine}{Exception}")
				.WriteTo.Debug()
				.WriteTo.File(Path.Combine(logsPath, "rt_archiver_.log"), rollingInterval: RollingInterval.Day)
				.CreateLogger();

			ArchivePath = archivePath;
			CachePath = cachePath;
			LogsPath = logsPath;
			VideosPath = videosPath;
			DatabasePath = databasePath;
			TempPath = tempPath;
			SitemapPath = sitemapPath;
			ImagesPath = imagesPath;
			UsersPath = usersPath;
			CommentsPath = commentsPath;
			BadgesPath = badgesPath;
			
			Log.Information($"Using archive directory {archivePath}");
			Log.Information($"Using cache directory {cachePath}");
			Log.Information($"Using logs directory {logsPath}");
			Log.Information($"Using videos directory {videosPath}");
			Log.Information($"Using database directory {databasePath}");
			Log.Information($"Using temp directory {tempPath}");
			Log.Information($"Using sitemap directory {sitemapPath}");
			Log.Information($"Using images directory {imagesPath}");
			Log.Information($"Using users directory {usersPath}");
			Log.Information($"Using comments directory {commentsPath}");
			Log.Information($"Using badges directory {badgesPath}");
		}
		catch (Exception err)
		{
			Log.Error(err, $"Could not create storage folders.");
			Environment.Exit(1);
		}
	}
}