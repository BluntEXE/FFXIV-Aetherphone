using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using Dalamud.Utility;
using SharpCompress.Archives;
using SharpCompress.Common;
using Newtonsoft.Json.Linq;

namespace Aetherphone.Core.Video;

internal sealed class Resources : IDisposable
{
	private readonly HttpClient _httpClient;
	private readonly string _configDir;

	internal string[] MpvCheckResult { get; private set; } = [string.Empty, string.Empty];
	internal string[] YtdlpCheckResult { get; private set; } = [string.Empty, string.Empty];

	private int _provisionStarted;

	internal Resources()
	{
		_httpClient = new HttpClient();
		_httpClient.DefaultRequestHeaders.Add("User-Agent", "AetherphoneAetherStreamUpdater/1.0");
		_configDir = Plugin.PluginInterface.ConfigDirectory.FullName;
	}

	public void Dispose()
	{
		_httpClient.Dispose();
		GC.SuppressFinalize(this);
	}

	internal string? GetLocationMPV()
	{
		string filenameStart = "mpv-dev-lgpl-x86_64-";
		string[] dirs = Directory.GetDirectories(_configDir, $"{filenameStart}*");
		if (dirs.Length > 0)
		{
			return dirs[0] + "/libmpv-2.dll";
		}
		else
		{
			return null;
		}
	}

	internal string? GetLocationYTDLP()
	{
		string filenameStart = "yt-dlp";
		string[] dirs = Directory.GetDirectories(_configDir, $"{filenameStart}*");
		if (dirs.Length > 0)
		{
			return dirs[0] + "/yt-dlp.exe";
		}
		else
		{
			return null;
		}
	}

	internal void EnsureProvisioned()
	{
		if (Interlocked.Exchange(ref _provisionStarted, 1) != 0)
		{
			return;
		}

		_ = ProvisionAsync();
	}

	private async Task ProvisionAsync()
	{
		if (GetLocationMPV() is null)
		{
			await CheckMPVAsync().ConfigureAwait(false);
			if (MpvCheckResult[0].Length > 0)
			{
				await DownloadMPVAsync().ConfigureAwait(false);
			}
		}

		if (GetLocationYTDLP() is null)
		{
			await CheckYTDLPAsync().ConfigureAwait(false);
			if (YtdlpCheckResult[0].Length > 0)
			{
				await DownloadYTDLPAsync().ConfigureAwait(false);
			}
		}
	}

	internal async Task CheckMPVAsync()
	{
		string filenameStart = "mpv-dev-lgpl-x86_64-";
		string filenameEnd = ".7z";
		string url = "https://api.github.com/repos/zhongfly/mpv-winbuild/releases/latest";
		MpvCheckResult = await CheckForUpdateAsync(_configDir, filenameStart, filenameEnd, url);
	}
	internal async Task CheckYTDLPAsync()
	{
		string filenameStart = "yt-dlp_x86.exe";
		string filenameEnd = ".exe";
		string url = "https://api.github.com/repos/yt-dlp/yt-dlp/releases/latest";
		YtdlpCheckResult = await CheckForUpdateAsync(_configDir, filenameStart, filenameEnd, url);
	}
	internal async Task<bool> DownloadMPVAsync()
	{
		string filenameStart = "mpv-dev-lgpl-x86_64-";
		string filenameEnd = ".7z";
		string downloadURL = MpvCheckResult[0];
		string folderName = MpvCheckResult[1];
		if (downloadURL.Length == 0)
		{
			return false;
		}

		return await UpdateAsync(_configDir, filenameStart, filenameEnd, downloadURL, folderName);
	}
	internal async Task<bool> DownloadYTDLPAsync()
	{
		string filenameStart = "yt-dlp";
		string filenameEnd = ".exe";
		string downloadURL = YtdlpCheckResult[0];
		string folderName = YtdlpCheckResult[1];
		if (downloadURL.Length == 0)
		{
			return false;
		}

		return await UpdateAsync(_configDir, filenameStart, filenameEnd, downloadURL, folderName);
	}
	private async Task<string[]> CheckForUpdateAsync(string configDir, string nameStartsWith, string nameEndsWith, string checkURL)
	{
		try{
			string json = await _httpClient.GetStringAsync(checkURL);
			var doc = JObject.Parse(json);
			long remoteId = doc["id"]!.Value<long>();
			var asset = doc["assets"]!
				.First(a => a["name"]!.Value<string>()!
					.StartsWith(nameStartsWith, StringComparison.Ordinal) &&
					a["name"]!.Value<string>()!.EndsWith(nameEndsWith, StringComparison.Ordinal));

			string assetName = asset["name"]!.Value<string>()!;
			string folderName = assetName.Replace(nameEndsWith, "") + "_" + remoteId;

			string localFolder = Path.Combine(configDir, folderName);

			if (Directory.Exists(localFolder))
			{
				return [string.Empty, folderName];
			}

			string downloadURL = asset["browser_download_url"]!.Value<string>()!;
			AepLog.Warning("Found Update: " + downloadURL);
			return [downloadURL, folderName];
		}
		catch (Exception exception)
		{
			AepLog.Warning("Failed to check for update (" + checkURL + "): " + exception);
			return [string.Empty, string.Empty];
		}
	}

	private async Task<bool> UpdateAsync(string configDir, string nameStartsWith, string nameEndsWith, string downloadURL, string folderName)
	{
		try
		{
			AepLog.Debug("Downloading Update: " + downloadURL);
			string tempFile = Path.GetTempFileName() + nameEndsWith;
			var response = await _httpClient.GetAsync(downloadURL, HttpCompletionOption.ResponseHeadersRead);
			await using (var fs = File.OpenWrite(tempFile))
			{
				await response.Content.CopyToAsync(fs);
			}
			AepLog.Debug("Finished Downloading " + downloadURL);
			if (nameEndsWith == ".7z")
			{
				string localFolder = Path.Combine(configDir, Path.GetRandomFileName());
				Directory.CreateDirectory(localFolder);
				using (var archive = ArchiveFactory.OpenArchive(tempFile))
				{
					foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
					{
						entry.WriteToDirectory(localFolder, new ExtractionOptions
						{
							ExtractFullPath = true,
							Overwrite = true
						});
					}
				}

				File.Delete(tempFile);

				foreach (string dir in Directory.GetDirectories(configDir, $"{nameStartsWith}*"))
				{
					Directory.Delete(dir, recursive: true);
				}

				if (Directory.Exists(Path.Combine(configDir, folderName)))
				{
					foreach (string file in Directory.GetFiles(localFolder, "*", SearchOption.AllDirectories))
					{
						string relative = Path.GetRelativePath(localFolder, file);
						string target = Path.Combine(Path.Combine(configDir, folderName), relative);
						Directory.CreateDirectory(Path.GetDirectoryName(target)!);
						File.Copy(file, target, overwrite: true);
					}
				}
				else
				{
					Directory.Move(localFolder, Path.Combine(configDir, folderName));
				}
			}
			else
			{
				foreach (string dir in Directory.GetDirectories(configDir, $"{nameStartsWith}*"))
				{
					Directory.Delete(dir, recursive: true);
				}

				string localFolder = Path.Combine(configDir, folderName);
				Directory.CreateDirectory(localFolder);

				string targetPath = Path.Combine(localFolder, nameStartsWith.EndsWith(nameEndsWith, StringComparison.Ordinal) ? nameStartsWith : nameStartsWith + nameEndsWith);
				File.Copy(tempFile, targetPath, overwrite: true);
				File.Delete(tempFile);
			}
			return true;
		}
		catch (Exception e)
		{
			AepLog.Error($"Error updating {nameStartsWith}: {e.Message} {e.StackTrace}");
			return false;
		}
	}

	internal static class NativeLoader
	{
		private static Resources? _resources;
		private static bool _registered;

		internal static void Register(Resources resources)
		{
			_resources = resources;
			if (_registered)
			{
				return;
			}

			_registered = true;
			NativeLibrary.SetDllImportResolver(typeof(NativeLoader).Assembly, Resolve);
		}

		private static IntPtr Resolve(string name, System.Reflection.Assembly assembly, DllImportSearchPath? path)
		{
			switch (name)
			{
				case "libmpv-2":
					return TryLoad(_resources?.GetLocationMPV(), "MPV");
				default:
					return IntPtr.Zero;
			}
		}

		private static IntPtr TryLoad(string? location, string tag)
		{
			if (location != null && NativeLibrary.TryLoad(location, out nint handle))
			{
				return handle;
			}
			AepLog.Error($"[{tag}] Failed to load native lib from: {location}");
			return IntPtr.Zero;
		}
	}
}
