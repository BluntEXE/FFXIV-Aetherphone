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
		string? dir = Directory.GetDirectories(_configDir, $"{filenameStart}*").FirstOrDefault();
		if (dir != null)
		{
			return dir + "/libmpv-2.dll";
		}
		else
		{
			return null;
		}
	}

	internal string? GetLocationYTDLP()
	{
		string filenameStart = "yt-dlp";
		string? dir = Directory.GetDirectories(_configDir, $"{filenameStart}*").FirstOrDefault();
		if (dir != null)
		{
			return dir + "/yt-dlp.exe";
		}
		else
		{
			return null;
		}
	}

	// mpv and yt-dlp expand to roughly 108 MB in the plugin config directory, so neither is
	// fetched at plugin load: this runs the first time someone actually reaches for AetherStream,
	// which is opening the app or a watch-along join starting playback. Anyone who never opens
	// the app downloads nothing at all.
	//
	// Only a binary that is missing outright is fetched, since there is no playback without it.
	// A build that already works is left alone and not even checked: mpv-winbuild publishes
	// nightly, so following it automatically meant re-downloading 26 MB most days, and a version
	// pin is no answer either because that repo keeps only about a month of releases before
	// pruning them, so a pinned tag would eventually 404 and strand new installs. Replacing a
	// working build is a deliberate tap in Settings instead. One-shot per session.
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
	// downloadURL is empty either because CheckForUpdateAsync already found the local folder up
	// to date, or because the check itself failed (rate limit, no network yet at plugin load) and
	// fell back to its empty-result default - either way there is nothing to fetch, and calling
	// HttpClient.GetAsync with an empty URI throws. Callers (AetherStreamApp.Settings) already
	// re-run the check first when this is empty, but this guard stays as the actual line that
	// can never hand HttpClient an invalid request.
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
				return [string.Empty, folderName]; //Already up to date
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

				if (Directory.Exists(Path.Combine(configDir, folderName))) //Super weird but lets just do this to be safe
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
					// Queried fresh rather than cached at startup - mpv-winbuild may still be
					// downloading (see CheckMPVAsync/DownloadMPVAsync) the first time this
					// resolves.
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
