#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows;
using WPF_Sorter_2._0.Contracts.Services;
using WPF_Sorter_2._0.Core.Models;

namespace WPF_Sorter_2._0.Services;

public class UpdateService : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _repoOwner = "NecroMagik";
    private readonly string _repoName = "WPF-Sorter-2.0";
    private readonly string _currentVersion;
    private readonly IToastNotificationsService _toastService;
    private bool _disposed = false;
    private bool _toastShownForCurrentUpdate = false;

    public event EventHandler<UpdateInfo>? UpdateAvailable;
    public event EventHandler? NoUpdateAvailable;

    public UpdateService(IApplicationInfoService applicationInfoService, IToastNotificationsService toastService)
    {
        _currentVersion = applicationInfoService.GetVersion();
        _toastService = toastService;

        var handler = new HttpClientHandler();
        _httpClient = new HttpClient(handler);
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
        _httpClient.DefaultRequestHeaders.UserAgent.TryParseAdd("WPF-Sorter-2.0");
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
    }

    public string GetCurrentVersion() => _currentVersion;

    public async Task<UpdateInfo?> CheckForUpdatesAsync(bool showToastIfAvailable = true)
    {
        try
        {
            Debug.WriteLine("=== 🔍 Checking for updates ===");
            Debug.WriteLine($"Current version: {_currentVersion}");

            var apiUrl = $"https://api.github.com/repos/{_repoOwner}/{_repoName}/releases/latest";
            Debug.WriteLine($"📡 Requesting: {apiUrl}");

            var response = await _httpClient.GetAsync(apiUrl);
            Debug.WriteLine($"📡 Response status: {response.StatusCode}");

            if (!response.IsSuccessStatusCode)
            {
                Debug.WriteLine($"❌ GitHub API error: {response.StatusCode}");
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            Debug.WriteLine($"📄 Response length: {json.Length} chars");

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            };

            var release = JsonSerializer.Deserialize<GitHubRelease>(json, options);

            if (release == null || string.IsNullOrEmpty(release.TagName))
            {
                Debug.WriteLine("❌ Failed to parse GitHub response");
                return null;
            }

            var latestVersion = release.TagName.TrimStart('v');
            var isNewer = CompareVersions(latestVersion, _currentVersion) > 0;

            Debug.WriteLine($"Latest version: {latestVersion}, Is newer: {isNewer}");

            if (!isNewer)
            {
                Debug.WriteLine("ℹ️ No updates available");
                _toastShownForCurrentUpdate = false;
                NoUpdateAvailable?.Invoke(this, EventArgs.Empty);
                return null;
            }

            var asset = FindBestAsset(release.Assets);
            var cleanChangelog = CleanChangelog(release.Body);

            string? assetHash = null;
            if (asset != null && !string.IsNullOrEmpty(asset.BrowserDownloadUrl))
            {
                Debug.WriteLine($"🔐 Getting hash for: {asset.BrowserDownloadUrl}");
                assetHash = await GetAssetHashAsync(asset.BrowserDownloadUrl);
                Debug.WriteLine($"🔐 Hash: {(string.IsNullOrEmpty(assetHash) ? "NOT FOUND" : assetHash)}");
            }

            var updateInfo = new UpdateInfo
            {
                Version = latestVersion,
                ReleaseDate = release.PublishedAt,
                DownloadUrl = asset?.BrowserDownloadUrl ?? string.Empty,
                Changelog = cleanChangelog,
                AssetName = asset?.Name ?? string.Empty,
                AssetSize = asset?.Size ?? 0,
                AssetHash = assetHash ?? string.Empty,
                IsPrerelease = release.Prerelease,
                IsNewer = true
            };

            Debug.WriteLine($"✅ Update found: {_currentVersion} -> {latestVersion}");

            if (showToastIfAvailable && !_toastShownForCurrentUpdate)
            {
                ShowUpdateToast(updateInfo);
                _toastShownForCurrentUpdate = true;
            }

            UpdateAvailable?.Invoke(this, updateInfo);
            return updateInfo;
        }
        catch (HttpRequestException ex)
        {
            Debug.WriteLine($"❌ HTTP Request error: {ex.Message}");
            Debug.WriteLine($"   Stack: {ex.StackTrace}");
            return null;
        }
        catch (TaskCanceledException ex)
        {
            Debug.WriteLine($"❌ Timeout error: {ex.Message}");
            return null;
        }
        catch (JsonException ex)
        {
            Debug.WriteLine($"❌ JSON parsing error: {ex.Message}");
            Debug.WriteLine($"   Stack: {ex.StackTrace}");
            return null;
        }
        catch (IOException ex)
        {
            Debug.WriteLine($"❌ IO error: {ex.Message}");
            Debug.WriteLine($"   Stack: {ex.StackTrace}");
            return null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ CheckForUpdates error: {ex.Message}");
            Debug.WriteLine($"   Type: {ex.GetType().Name}");
            Debug.WriteLine($"   Stack: {ex.StackTrace}");
            return null;
        }
    }

    private async Task<string?> GetAssetHashAsync(string downloadUrl)
    {
        try
        {
            var hashUrl = downloadUrl + ".sha256";
            Debug.WriteLine($"🔐 Requesting hash from: {hashUrl}");

            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.UserAgent.TryParseAdd("WPF-Sorter-2.0");

            var response = await client.GetAsync(hashUrl);
            Debug.WriteLine($"🔐 Hash response status: {response.StatusCode}");

            if (response.IsSuccessStatusCode)
            {
                var hash = await response.Content.ReadAsStringAsync();
                hash = hash.Trim().Replace("\n", "").Replace("\r", "").Replace(" ", "").ToLowerInvariant();
                Debug.WriteLine($"🔐 Hash received: {hash}");
                return hash;
            }
            else
            {
                Debug.WriteLine($"⚠️ Hash file not found (status: {response.StatusCode})");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"⚠️ Could not get hash: {ex.Message}");
        }
        return null;
    }

    public async Task<string> DownloadUpdateAsync(UpdateInfo updateInfo, IProgress<int>? progress = null)
    {
        if (string.IsNullOrEmpty(updateInfo.DownloadUrl))
            throw new InvalidOperationException("Download URL is empty");

        Debug.WriteLine($"📁 Starting download...");
        Debug.WriteLine($"   URL: {updateInfo.DownloadUrl}");
        Debug.WriteLine($"   Expected hash: {updateInfo.AssetHash}");

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var workingFolder = Path.Combine(Path.GetTempPath(), $"WPF-Sorter-2.0-Update_{timestamp}");

        Debug.WriteLine($"📁 Working folder: {workingFolder}");

        try
        {
            if (!Directory.Exists(workingFolder))
            {
                Directory.CreateDirectory(workingFolder);
                Debug.WriteLine($"📁 Created folder");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ Failed to create folder: {ex.Message}");
            throw;
        }

        var tempFilePath = Path.Combine(workingFolder, $"{updateInfo.AssetName}.tmp");
        var finalFilePath = Path.Combine(workingFolder, updateInfo.AssetName);

        Debug.WriteLine($"   Temp file: {tempFilePath}");
        Debug.WriteLine($"   Final file: {finalFilePath}");

        try
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromMinutes(5);
            client.DefaultRequestHeaders.UserAgent.TryParseAdd("WPF-Sorter-2.0");

            Debug.WriteLine($"⬇️ Downloading...");
            using var response = await client.GetAsync(updateInfo.DownloadUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1;
            Debug.WriteLine($"   Total bytes: {totalBytes}");

            var bytesRead = 0L;
            var buffer = new byte[8192];
            var lastProgress = 0;

            using var contentStream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.Read);

            while (true)
            {
                var read = await contentStream.ReadAsync(buffer, 0, buffer.Length);
                if (read == 0) break;

                await fileStream.WriteAsync(buffer, 0, read);
                bytesRead += read;

                if (totalBytes > 0)
                {
                    var currentProgress = (int)((double)bytesRead / totalBytes * 100);
                    if (currentProgress != lastProgress && currentProgress % 5 == 0)
                    {
                        progress?.Report(currentProgress);
                        lastProgress = currentProgress;
                    }
                }
            }

            await fileStream.FlushAsync();
            fileStream.Close();
            progress?.Report(100);

            Debug.WriteLine($"✅ Download complete: {bytesRead} bytes");

            var fileInfo = new FileInfo(tempFilePath);
            if (fileInfo.Length == 0)
            {
                throw new Exception("Downloaded file is empty");
            }

            // Проверка ХЭША
            if (!string.IsNullOrEmpty(updateInfo.AssetHash))
            {
                Debug.WriteLine($"🔐 Verifying hash...");
                using var sha256 = SHA256.Create();
                using var stream = File.OpenRead(tempFilePath);
                var hashBytes = await sha256.ComputeHashAsync(stream);
                var actualHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();

                Debug.WriteLine($"   Expected: {updateInfo.AssetHash}");
                Debug.WriteLine($"   Actual:   {actualHash}");

                if (!string.Equals(actualHash, updateInfo.AssetHash, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"Hash mismatch!\nExpected: {updateInfo.AssetHash}\nActual: {actualHash}");
                }
                Debug.WriteLine($"✅ Hash verification passed!");
            }
            else
            {
                Debug.WriteLine($"⚠️ No hash to verify");
            }

            // Переименование
            Debug.WriteLine($"📝 Renaming file...");
            if (File.Exists(finalFilePath))
            {
                Debug.WriteLine($"   Removing existing file: {finalFilePath}");
                try { File.Delete(finalFilePath); }
                catch (Exception ex) { Debug.WriteLine($"   ⚠️ Could not delete: {ex.Message}"); }
            }

            File.Move(tempFilePath, finalFilePath);
            Debug.WriteLine($"✅ File saved: {finalFilePath}");

            return finalFilePath;
        }
        catch (IOException ex)
        {
            Debug.WriteLine($"❌ IO error during download: {ex.Message}");
            Debug.WriteLine($"   Stack: {ex.StackTrace}");
            try { if (File.Exists(tempFilePath)) File.Delete(tempFilePath); } catch { }
            throw;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ Download error: {ex.Message}");
            Debug.WriteLine($"   Stack: {ex.StackTrace}");
            try { if (File.Exists(tempFilePath)) File.Delete(tempFilePath); } catch { }
            throw;
        }
    }

    public void ResetToastFlag()
    {
        _toastShownForCurrentUpdate = false;
        Debug.WriteLine("🔄 Toast flag reset");
    }

    private void ShowUpdateToast(UpdateInfo updateInfo)
    {
        try
        {
            Debug.WriteLine($"📢 Showing toast for version {updateInfo.Version}");

            var toastContent = new CommunityToolkit.WinUI.Notifications.ToastContent
            {
                Visual = new CommunityToolkit.WinUI.Notifications.ToastVisual
                {
                    BindingGeneric = new CommunityToolkit.WinUI.Notifications.ToastBindingGeneric
                    {
                        Children =
                        {
                            new CommunityToolkit.WinUI.Notifications.AdaptiveText
                            {
                                Text = $"Новая версия {updateInfo.Version} доступна!"
                            },
                            new CommunityToolkit.WinUI.Notifications.AdaptiveText
                            {
                                Text = "Нажмите, чтобы установить обновление"
                            }
                        }
                    }
                },
                Actions = new CommunityToolkit.WinUI.Notifications.ToastActionsCustom
                {
                    Buttons =
                    {
                        new CommunityToolkit.WinUI.Notifications.ToastButton("Обновить", "update_install")
                        {
                            ActivationType = CommunityToolkit.WinUI.Notifications.ToastActivationType.Foreground
                        },
                        new CommunityToolkit.WinUI.Notifications.ToastButton("Отложить", "update_later")
                        {
                            ActivationType = CommunityToolkit.WinUI.Notifications.ToastActivationType.Background
                        }
                    }
                },
                Launch = $"update_available|{updateInfo.Version}"
            };

            var doc = new Windows.Data.Xml.Dom.XmlDocument();
            doc.LoadXml(toastContent.GetContent());
            var toast = new Windows.UI.Notifications.ToastNotification(doc)
            {
                Tag = "UpdateNotification",
                Group = "Updates",
                ExpirationTime = DateTime.Now.AddSeconds(30)
            };

            _toastService.ShowToastNotification(toast);
            Debug.WriteLine($"✅ Toast shown");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ Failed to show toast: {ex.Message}");
            Debug.WriteLine($"   Stack: {ex.StackTrace}");
        }
    }

    public async Task DownloadAndInstallUpdateAsync(UpdateInfo updateInfo)
    {
        Debug.WriteLine($"🚀 DownloadAndInstallUpdateAsync called");
        var filePath = await DownloadUpdateAsync(updateInfo, null);
        InstallUpdate(filePath);
    }

    public void InstallUpdate(string filePath)
    {
        Debug.WriteLine($"🚀 InstallUpdate called: {filePath}");

        if (!File.Exists(filePath))
        {
            Debug.WriteLine($"❌ File not found: {filePath}");
            throw new FileNotFoundException($"Update file not found: {filePath}");
        }

        var tempPath = Path.GetDirectoryName(filePath) ?? Path.GetTempPath();
        Debug.WriteLine($"   Temp path: {tempPath}");

        var installScript = CreateInstallScript(filePath, tempPath);
        Debug.WriteLine($"   Install script: {installScript}");

        var logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "ZeN", "Sorter");
        if (!Directory.Exists(logDir))
        {
            Directory.CreateDirectory(logDir);
            Debug.WriteLine($"📁 Created log directory: {logDir}");
        }

        var batPath = Path.Combine(tempPath, "install.bat");
        var batContent = $"@echo off\r\n" +
                         $"echo ========================================\r\n" +
                         $"echo WPF-Sorter-2.0 Update Installer\r\n" +
                         $"echo ========================================\r\n" +
                         $"echo.\r\n" +
                         $"echo Log file: {logDir}\\update_log_*.txt\r\n" +
                         $"echo.\r\n" +
                         $"echo Starting update...\r\n" +
                         $"echo.\r\n" +
                         $"powershell.exe -ExecutionPolicy Bypass -File \"{installScript}\"\r\n" +
                         $"echo.\r\n" +
                         $"echo ========================================\r\n" +
                         $"echo Update completed.\r\n" +
                         $"echo ========================================\r\n" +
                         $"echo.\r\n" +
                         $"pause";

        File.WriteAllText(batPath, batContent, Encoding.UTF8);
        Debug.WriteLine($"✅ BAT created: {batPath}");

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = batPath,
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Normal,
                CreateNoWindow = false
            }
        };

        Debug.WriteLine($"🚀 Starting update process...");
        Debug.WriteLine($"   Command: {batPath}");

        Application.Current?.Shutdown();
        process.Start();
    }

    private string CreateInstallScript(string updateFile, string tempPath)
    {
        var scriptPath = Path.Combine(tempPath, "install.ps1");
        var appDir = AppDomain.CurrentDomain.BaseDirectory;
        var updateFileName = Path.GetFileName(updateFile);

        var logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "ZeN", "Sorter");
        if (!Directory.Exists(logDir)) Directory.CreateDirectory(logDir);

        var logFile = Path.Combine(logDir, $"update_log_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt");

        Debug.WriteLine($"📝 Creating install script: {scriptPath}");
        Debug.WriteLine($"   Log file: {logFile}");

        var scriptContent =
            "# Update Script\r\n" +
            $"$logFile = '{logFile}'\r\n" +
            $"$updateFile = '{updateFile}'\r\n" +
            $"$appDir = '{appDir}'\r\n" +
            $"$tempPath = '{tempPath}'\r\n" +
            "\r\n" +
            "function Write-Log {\r\n" +
            "    param([string]$Message)\r\n" +
            "    $timestamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'\r\n" +
            "    $logMessage = \"[$timestamp] $Message\"\r\n" +
            "    Write-Host $logMessage\r\n" +
            "    Add-Content -Path $logFile -Value $logMessage\r\n" +
            "}\r\n" +
            "\r\n" +
            "Write-Log '========== START UPDATE =========='\r\n" +
            "Write-Log \"Update file: $updateFile\"\r\n" +
            "Write-Log \"App directory: $appDir\"\r\n" +
            "\r\n" +
            "if (-not (Test-Path $updateFile)) {\r\n" +
            "    Write-Log '❌ Update file not found'\r\n" +
            "    exit 1\r\n" +
            "}\r\n" +
            "\r\n" +
            "$extractPath = Join-Path $tempPath 'Extract'\r\n" +
            "Write-Log \"Extract path: $extractPath\"\r\n" +
            "if (Test-Path $extractPath) { Remove-Item $extractPath -Recurse -Force }\r\n" +
            "New-Item -ItemType Directory -Path $extractPath -Force | Out-Null\r\n" +
            "\r\n" +
            "try {\r\n" +
            "    if ($updateFile -match '\\.zip$') {\r\n" +
            "        Write-Log '📦 Extracting ZIP...'\r\n" +
            "        Expand-Archive -Path $updateFile -DestinationPath $extractPath -Force\r\n" +
            "        Write-Log '✅ ZIP extracted'\r\n" +
            "        $sourceDir = $extractPath\r\n" +
            "    } else {\r\n" +
            "        Write-Log '📄 Copying EXE...'\r\n" +
            "        Copy-Item -Path $updateFile -Destination $extractPath -Force\r\n" +
            "        Write-Log '✅ EXE copied'\r\n" +
            "        $sourceDir = $extractPath\r\n" +
            "    }\r\n" +
            "\r\n" +
            "    Write-Log '🔄 Copying files...'\r\n" +
            "    Copy-Item -Path \"$sourceDir\\*\" -Destination $appDir -Recurse -Force\r\n" +
            "    Write-Log '✅ Files copied!'\r\n" +
            "\r\n" +
            "    Write-Log '🚀 Launching...'\r\n" +
            "    Start-Process -FilePath (Join-Path $appDir 'WPF-Sorter-2.0.exe')\r\n" +
            "\r\n" +
            "    Write-Log '🧹 Cleaning up...'\r\n" +
            "    Remove-Item -Path $tempPath -Recurse -Force -ErrorAction SilentlyContinue\r\n" +
            "\r\n" +
            "    Write-Log '========== UPDATE SUCCESSFUL =========='\r\n" +
            "    exit 0\r\n" +
            "} catch {\r\n" +
            "    Write-Log \"❌ ERROR: $_\"\r\n" +
            "    Write-Log \"   Stack: $($_.ScriptStackTrace)\"\r\n" +
            "    exit 1\r\n" +
            "}\r\n";

        File.WriteAllText(scriptPath, scriptContent, Encoding.UTF8);
        Debug.WriteLine($"✅ Install script created");

        return scriptPath;
    }

    private int CompareVersions(string version1, string version2)
    {
        try
        {
            var v1Parts = version1.Split('.');
            var v2Parts = version2.Split('.');

            for (int i = 0; i < Math.Max(v1Parts.Length, v2Parts.Length); i++)
            {
                int v1Num = i < v1Parts.Length && int.TryParse(v1Parts[i], out var v1) ? v1 : 0;
                int v2Num = i < v2Parts.Length && int.TryParse(v2Parts[i], out var v2) ? v2 : 0;

                if (v1Num != v2Num)
                    return v1Num.CompareTo(v2Num);
            }

            return 0;
        }
        catch
        {
            return string.Compare(version1, version2, StringComparison.Ordinal);
        }
    }

    private string CleanChangelog(string? body)
    {
        if (string.IsNullOrEmpty(body))
            return "Нет описания изменений.";

        var cleaned = body
            .Replace("🚀", "").Replace("📦", "").Replace("🪟", "")
            .Replace("📂", "").Replace("🔐", "").Replace("✅", "")
            .Replace("⚠️", "").Replace("🎉", "").Replace("💪", "")
            .Replace("🔥", "").Replace("##", "").Replace("###", "")
            .Replace("**", "").Replace("___", "").Replace("---", "")
            .Replace("—", "-").Replace("\r", "").Trim();

        var lines = cleaned.Split('\n')
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => line.Trim())
            .ToList();

        return lines.Count == 0 ? "Нет описания изменений." : string.Join("\n", lines);
    }

    private GitHubAsset? FindBestAsset(List<GitHubAsset> assets)
    {
        Debug.WriteLine($"🔍 Looking for best asset in {assets.Count} assets");

        foreach (var asset in assets)
        {
            Debug.WriteLine($"   Asset: {asset.Name} (Size: {asset.Size})");
            if (asset.Name?.EndsWith(".exe") == true && !asset.Name.Contains("Portable"))
            {
                Debug.WriteLine($"   ✅ Selected EXE: {asset.Name}");
                return asset;
            }
        }
        foreach (var asset in assets)
        {
            if (asset.Name?.EndsWith(".zip") == true)
            {
                Debug.WriteLine($"   ✅ Selected ZIP: {asset.Name}");
                return asset;
            }
        }

        Debug.WriteLine($"   ❌ No suitable asset found");
        return null;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _httpClient?.Dispose();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}

public class GitHubRelease
{
    [JsonPropertyName("tag_name")]
    public string TagName { get; set; } = string.Empty;

    [JsonPropertyName("published_at")]
    public string PublishedAt { get; set; } = string.Empty;

    [JsonPropertyName("body")]
    public string Body { get; set; } = string.Empty;

    [JsonPropertyName("prerelease")]
    public bool Prerelease { get; set; }

    [JsonPropertyName("assets")]
    public List<GitHubAsset> Assets { get; set; } = new();
}

public class GitHubAsset
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("browser_download_url")]
    public string BrowserDownloadUrl { get; set; } = string.Empty;

    [JsonPropertyName("size")]
    public long Size { get; set; }
}