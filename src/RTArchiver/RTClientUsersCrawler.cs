using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Diagnostics;
using System.Net;
using System.Reflection.Metadata;
using System.Text;
using System.Text.Json;
using RTArchiver.Data;
using RTArchiver.Data.Responses;
using Serilog;

namespace RTArchiver;

public class RTClientUsersCrawler
{
	RTClient _rtClient;
	ConcurrentBag<string> _usersToFetch = new ConcurrentBag<string>();
	List<Thread> _threads = new List<Thread>();
	public RTClientUsersCrawler(RTClient rtClient)
	{
		_rtClient = rtClient;
	}

	public async Task StartAndWaitAsync()
	{
		Log.Information($"Starting: RTClientUsersCrawler with {_rtClient.NumberOfThreads} threads.");

		var parallelOptions = new ParallelOptions()
		{
			MaxDegreeOfParallelism = _rtClient.NumberOfThreads ,
		};

		FrozenDictionary<string, string>? frozenDictionary = null;
		
		// We try save into this path so we don't have to load every comment every time.
		var commentsUsersFile = Path.Combine(Storage.CachePath, "comments_users.json");
		if (_rtClient.UseCache && File.Exists(commentsUsersFile))
		{

			using (var fileStream = File.OpenRead(commentsUsersFile))
			{
				var tempDict = JsonSerializer.Deserialize<Dictionary<string, string>>(fileStream);
				if (tempDict != null)
				{
					frozenDictionary = tempDict.ToFrozenDictionary();
				}
			}
		}
		
		if (frozenDictionary is null)
		{
			var jsonFiles = Directory.GetFiles(Path.Combine(Storage.CommentsPath), "*.json", SearchOption.AllDirectories);
			
            var tempDict = new Dictionary<string, string>();
            var listLock = new object();
            
            await Parallel.ForEachAsync(jsonFiles, parallelOptions, async (jsonFile, state) =>
            {
	            // If a comment is only 85bytes long there is nothing here to load.
            	var fileInfo = new FileInfo(jsonFile);
            	if (fileInfo.Length == 85)
            	{
            		// No comments.
            		return;
            	}
    
            	Log.Information($"Opening {jsonFile}");
            	CommentsResponse? commentsResponse = null;
            	try
            	{
    
            		using (var fileStream = File.OpenRead(jsonFile))
            		{
            			commentsResponse = await JsonSerializer.DeserializeAsync<CommentsResponse>(fileStream, cancellationToken: state).ConfigureAwait(false);
            			if (commentsResponse is null)
            			{
            				Log.Error($"commentsResponse was null");
            				Debugger.Break();
            				return;
            			}
    
            			if (commentsResponse.Data.Count == 0)
            			{
            				Log.Error($"Zero comments found - {jsonFile}");
            				Debugger.Break();
            				return;
            			}
            		}
            	
            	}
            	catch (Exception err)
            	{
            		Log.Error(err, $"Error opening {jsonFile}");
            		Debugger.Break();
            		return;
            	}
            	
    
            	lock (listLock)
            	{
            		foreach (var comment in commentsResponse.Data)
            		{
            			if (tempDict.ContainsKey(comment.OwnerUuid))
            			{
            				continue;
            			}
    
            			tempDict[comment.OwnerUuid] = comment.OwnerName;
            		}
            	}
            });

            using (var fileStream = File.Create(commentsUsersFile))
            {
	            JsonSerializer.Serialize(fileStream, tempDict);
            }
            
            frozenDictionary = tempDict.ToFrozenDictionary();
		}

		if (frozenDictionary is null)
		{
			Log.Error("FrozenDictionary was null.");
			return;
		}

		var count = 0;
		var length = frozenDictionary.Keys.Length;
		await Parallel.ForEachAsync(frozenDictionary.Keys, parallelOptions, async (ownerUuid, state) =>
		{
			++count;
			Log.Information($"{count} / {length}");
			
			var ownerDirectory = Path.Combine(Storage.UsersPath, ownerUuid);
			if (Directory.Exists(ownerDirectory) && Directory.GetFiles(ownerDirectory).Length > 0)
			{
				return;
			}
			
			try
			{			
				if (Directory.Exists(ownerDirectory) == false)
				{
					Directory.CreateDirectory(ownerDirectory);
				}

				var hasDisplayPicture = false;

				var ownerCommunityUserJson = Path.Combine(ownerDirectory, "community_user.json");
				var ownerUserJson = Path.Combine(ownerDirectory, "user.json");

				CommunityUserResponse? communityUserResponse = null;
				if (File.Exists(ownerCommunityUserJson))
				{
					using (var fileStream = File.OpenRead(ownerCommunityUserJson))
					{
						communityUserResponse = await JsonSerializer.DeserializeAsync<CommunityUserResponse>(fileStream, cancellationToken: state).ConfigureAwait(false);
					}
				}

				var ownerName = frozenDictionary[ownerUuid];

				if (communityUserResponse is null)
				{
					Log.Information($"Loading community user {ownerUuid} - {ownerName}");

					var response = await _rtClient.HttpClient.GetAsync($"https://community.roosterteeth.com/api/v1/users/{ownerUuid}", cancellationToken: state);
					if (response.StatusCode == HttpStatusCode.OK)
					{
						using (var memoryStream = new MemoryStream())
						{
							using (var stream = await response.Content.ReadAsStreamAsync())
							{
								await stream.CopyToAsync(memoryStream);
							}

							memoryStream.Position = 0;

							using (var fileStream = File.Create(ownerCommunityUserJson))
							{
								await memoryStream.CopyToAsync(fileStream);
							}

							memoryStream.Position = 0;

							communityUserResponse = await JsonSerializer.DeserializeAsync<CommunityUserResponse>(memoryStream, cancellationToken: state).ConfigureAwait(false);
						}
					}
					else if (response.StatusCode == HttpStatusCode.TooManyRequests)
					{
						Log.Information($"{ownerUuid} community.roosterteeth.com too many requests.");
						await Task.Delay(60 * 1000);
					}
					else if (response.StatusCode == HttpStatusCode.NotFound)
					{
						Log.Information($"{ownerUuid} not found.");
					}
					else
					{
						Log.Error($"Invalid status code for {ownerUuid} - {response.StatusCode}");
						return;
					}
				}

				if (communityUserResponse is not null)
				{
					var displayPictureUrl = communityUserResponse.Data.ProfilePicture;
					if (String.IsNullOrEmpty(displayPictureUrl) == false)
					{
						var extension = Path.GetExtension(displayPictureUrl);
						var displayPicturePath = Path.Combine(ownerDirectory, $"display_picture{extension}");
						if (File.Exists(displayPicturePath))
						{
							hasDisplayPicture = true;
						}
						else
						{
							Log.Information($"Downloading display picture for {ownerUuid}");
							var response = await _rtClient.HttpClient.GetAsync(displayPictureUrl, cancellationToken: state);
							if (response.StatusCode == HttpStatusCode.OK)
							{
								using (var fileStream = File.Create(displayPicturePath))
								{
									using (var stream = await response.Content.ReadAsStreamAsync())
									{
										await stream.CopyToAsync(fileStream);
									}
								}

								hasDisplayPicture = true;
							}
							else if (response.StatusCode == HttpStatusCode.TooManyRequests)
							{
								Log.Information($"{ownerUuid} display picture too many requests.");
								await Task.Delay(60 * 1000);
							}
							else
							{
								Log.Error($"Invalid status code for {displayPictureUrl} - {response.StatusCode}");
							}
						}
					}
					else
					{
						Log.Error($"User does not have a display picture. ({ownerUuid})");
					}
				}

				User? user = null;
				Log.Information($"For slow: https://business-service.roosterteeth.com/api/v1/users/by_username/{ownerName}");
				
				// TODO: Re-write this to go through and run every 500ms
				/*
				ownerUserJson = Path.Combine(ownerDirectory, "user.json");
				if (File.Exists(ownerUserJson) == false)
				{
					Log.Information($"Loading user {ownerUuid} - {ownerName}");

					var response = await _rtClient.HttpClient.GetAsync($"https://business-service.roosterteeth.com/api/v1/users/by_username/{ownerName}", cancellationToken: state);
					if (response.StatusCode == HttpStatusCode.OK)
					{
						await Task.Delay(500);
						using (var memoryStream = new MemoryStream())
						{
							using (var stream = await response.Content.ReadAsStreamAsync())
							{
								await stream.CopyToAsync(memoryStream);
							}

							memoryStream.Position = 0;

							using (var fileStream = File.Create(ownerUserJson))
							{
								await memoryStream.CopyToAsync(fileStream);
							}

							memoryStream.Position = 0;

							user = await JsonSerializer.DeserializeAsync<User>(memoryStream, cancellationToken: state).ConfigureAwait(false);
						}
					}
					else if (response.StatusCode == HttpStatusCode.NotFound)
					{
						Log.Information($"{ownerUuid} not found.");
					}
					else if (response.StatusCode == HttpStatusCode.TooManyRequests)
					{
						var stringBuilder = new StringBuilder();
						foreach (var header in response.Headers)
						{
							stringBuilder.AppendLine($"{header.Key} - {string.Join(", ", header.Value)}");
						}
						Log.Information($"{ownerUuid} too many requests.\n{stringBuilder}");
						
						await Task.Delay(65 * 1000);
					}
					else
					{
						Log.Error($"Invalid status code for {ownerUuid} - {response.StatusCode}");
						return;
					}
				}
				else
				{
					using (var fileStream = File.OpenRead(ownerUserJson))
					{
						user = await JsonSerializer.DeserializeAsync<User>(fileStream, cancellationToken: state).ConfigureAwait(false);
					}
				}

				if (user is null)
				{
					Log.Error($"Could not load user {ownerUuid}");
				}
				else
				{
					if (hasDisplayPicture == false)
					{
						if (string.IsNullOrEmpty(user.Attributes.Pictures.Original.Profile) == false)
						{
							var displayPictureUrl = user.Attributes.Pictures.Original.Profile;
							var extension = Path.GetExtension(displayPictureUrl);
							var displayPicturePath = Path.Combine(ownerDirectory, $"display_picture{extension}");

							Log.Information($"Downloading display picture for {ownerUuid}");
							var response = await _rtClient.HttpClient.GetAsync(displayPictureUrl, cancellationToken: state);
							if (response.StatusCode == HttpStatusCode.OK)
							{
								using (var fileStream = File.Create(displayPicturePath))
								{
									using (var stream = await response.Content.ReadAsStreamAsync())
									{
										await stream.CopyToAsync(fileStream);
									}
								}

								hasDisplayPicture = true;
							}
							else
							{
								Log.Error($"Invalid status code for {displayPictureUrl} - {response.StatusCode}");
							}

						}
					}

					{
						var coverPictureUrl = user.Attributes.Pictures.Original.Cover;
						var extension = Path.GetExtension(coverPictureUrl);
						var coverPicturePath = Path.Combine(ownerDirectory, $"cover_picture{extension}");

						if (Path.Exists(coverPicturePath) == false)
						{
							Log.Information($"Downloading cover picture for {ownerUuid}");
							var response = await _rtClient.HttpClient.GetAsync(coverPictureUrl, cancellationToken: state);
							if (response.StatusCode == HttpStatusCode.OK)
							{
								using (var fileStream = File.Create(coverPicturePath))
								{
									using (var stream = await response.Content.ReadAsStreamAsync())
									{
										await stream.CopyToAsync(fileStream);
									}
								}
							}
							else
							{
								Log.Error($"Invalid status code for {coverPicturePath} - {response.StatusCode}");
							}
						}
					}
				}
				*/
			}
			catch (Exception err)
			{
				Log.Error(err, $"Could not fetch ownerUuid {ownerUuid}.");
			}
		});
		
		Log.Information($"Found {frozenDictionary.Keys.Length} users");
		//165308
	}
}