using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows;
using WPF_Sorter_2._0.Contracts.Services;
using WPF_Sorter_2._0.Core.Models;

namespace WPF_Sorter_2._0.Services;

public class UpdateService
{
    private readonly HttpClient _httpClient;
    private readonly string _repoOwner = "NecroMagik";
    private readonly string _repoName = "WPF-Sorter-2.0";
    private readonly string _currentVersion;
    private readonly IToastNotificationsService _toastService;

    private bool _toastShownForCurrentUpdate = false;
    private UpdateInfo? _lastUpdateInfo;

    public event EventHandler<UpdateInfo>? UpdateAvailable;
    public event EventHandler? NoUpdateAvailable;

    public UpdateService(IApplicationInfoService applicationInfoService, IToastNotificationsService toastService)
    {
        _currentVersion = applicationInfoService.GetVersion();
        _toastService = toastService;

        System.Net.ServicePointManager.SecurityProtocol =
        System.Net.SecurityProtocolType.Tls12 |
        System.Net.SecurityProtocolType.Tls13;

        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.UserAgent.TryParseAdd("WPF-Sorter-2.0");
        _httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
    }

    public string GetCurrentVersion()
    {
        return _currentVersion;
    }

    public async Task<UpdateInfo?> CheckForUpdatesAsync(bool showToastIfAvailable = true)
    {
        try
        {
            Debug.WriteLine("=== 🔍 Checking for updates ===");
            Debug.WriteLine($"Current version: {_currentVersion}");

            var apiUrl = $"https://api.github.com/repos/{_repoOwner}/{_repoName}/releases/latest";
            var response = await _httpClient.GetAsync(apiUrl);

            if (!response.IsSuccessStatusCode)
            {
                Debug.WriteLine($"❌ GitHub API error: {response.StatusCode}");
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
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
                _lastUpdateInfo = null;
                NoUpdateAvailable?.Invoke(this, EventArgs.Empty);
                return null;
            }

            var asset = FindBestAsset(release.Assets);
            var cleanChangelog = CleanChangelog(release.Body);

            var updateInfo = new UpdateInfo
            {
                Version = latestVersion,
                ReleaseDate = release.PublishedAt,
                DownloadUrl = asset?.BrowserDownloadUrl ?? string.Empty,
                Changelog = cleanChangelog,
                AssetName = asset?.Name ?? string.Empty,
                AssetSize = asset?.Size ?? 0,
                IsPrerelease = release.Prerelease,
                IsNewer = true
            };

            Debug.WriteLine($"✅ Update found: {_currentVersion} -> {latestVersion}");

            if (showToastIfAvailable && !_toastShownForCurrentUpdate)
            {
                ShowUpdateToast(updateInfo);
                _toastShownForCurrentUpdate = true;
                Debug.WriteLine($"📢 Toast shown (first time for this update)");
            }
            else if (showToastIfAvailable && _toastShownForCurrentUpdate)
            {
                Debug.WriteLine($"ℹ️ Toast already shown for this update, skipping");
            }

            UpdateAvailable?.Invoke(this, updateInfo);
            return updateInfo;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ CheckForUpdates error: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Сбрасывает флаг показа Toast (вызывать при ручной проверке в настройках)
    /// </summary>
    public void ResetToastFlag()
    {
        _toastShownForCurrentUpdate = false;
        Debug.WriteLine("🔄 Toast flag reset");
    }

    private void ShowUpdateToast(UpdateInfo updateInfo)
    {
        try
        {
            var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "SorterLOGO.ico");

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
            Debug.WriteLine($"📢 Toast notification shown for version {updateInfo.Version}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ Failed to show toast: {ex.Message}");
        }
    }

    /// <summary>
    /// Скачивает обновление
    /// </summary>
    public async Task<string> DownloadUpdateAsync(UpdateInfo updateInfo, IProgress<int>? progress = null)
    {
        if (string.IsNullOrEmpty(updateInfo.DownloadUrl))
            throw new InvalidOperationException("Download URL is empty");

        var tempFolder = Path.Combine(Path.GetTempPath(), "WPF-Sorter-2.0-Update");
        Directory.CreateDirectory(tempFolder);

        var filePath = Path.Combine(tempFolder, updateInfo.AssetName);

        try
        {
            // 👇 ИСПОЛЬЗУЕМ WebClient
            using var client = new System.Net.WebClient();
            client.Headers.Add("User-Agent", "WPF-Sorter-2.0");

            // 👇 ПРОГРЕСС СКАЧИВАНИЯ
            if (progress != null)
            {
                client.DownloadProgressChanged += (s, e) =>
                {
                    progress.Report(e.ProgressPercentage);
                    Debug.WriteLine($"⬇️ Download progress: {e.ProgressPercentage}%");
                };
            }

            // 👇 СКАЧИВАЕМ ФАЙЛ
            await client.DownloadFileTaskAsync(new Uri(updateInfo.DownloadUrl), filePath);

            Debug.WriteLine($"✅ File downloaded: {filePath}");
            return filePath;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ Download error: {ex.Message}");
            throw;
        }
    }


    /// <summary>
    /// Скачивает и устанавливает обновление
    /// </summary>
    public async Task DownloadAndInstallUpdateAsync(UpdateInfo updateInfo)
    {
        try
        {
            var progress = new Progress<int>(p =>
            {
                Debug.WriteLine($"⬇️ Download progress: {p}%");
            });

            var filePath = await DownloadUpdateAsync(updateInfo, progress);
            InstallUpdate(filePath);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"❌ Download error: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Запускает установщик обновления
    /// </summary>
    public void InstallUpdate(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Update file not found: {filePath}");

        var tempPath = Path.Combine(Path.GetTempPath(), "WPF-Sorter-2.0-Update");
        var installScript = CreateInstallScript(filePath, tempPath);

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-ExecutionPolicy Bypass -File \"{installScript}\"",
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Normal
            }
        };

        Application.Current?.Shutdown();
        process.Start();
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
            .Replace("🚀", "")
            .Replace("📦", "")
            .Replace("🪟", "")
            .Replace("📂", "")
            .Replace("🔐", "")
            .Replace("✅", "")
            .Replace("⚠️", "")
            .Replace("🎉", "")
            .Replace("💪", "")
            .Replace("🔥", "")
            .Replace("##", "")
            .Replace("###", "")
            .Replace("**", "")
            .Replace("___", "")
            .Replace("---", "")
            .Replace("—", "-")
            .Replace("\r", "")
            .Trim();

        var lines = cleaned.Split('\n')
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => line.Trim())
            .ToList();

        if (lines.Count == 0)
            return "Нет описания изменений.";

        return string.Join("\n", lines);
    }

    private GitHubAsset? FindBestAsset(List<GitHubAsset> assets)
    {
        foreach (var asset in assets)
        {
            if (asset.Name?.EndsWith(".exe") == true && !asset.Name.Contains("Portable"))
                return asset;
        }

        foreach (var asset in assets)
        {
            if (asset.Name?.EndsWith(".zip") == true)
                return asset;
        }

        return null;
    }

    private string CreateInstallScript(string updateFile, string tempPath)
    {
        var scriptPath = Path.Combine(tempPath, "install.ps1");
        var appDir = AppDomain.CurrentDomain.BaseDirectory;

        var scriptContent = $@"
Write-Host '📦 Начинаем установку обновления...' -ForegroundColor Cyan

$updateFile = '{updateFile}'
$appDir = '{appDir}'
$tempPath = '{tempPath}'

Write-Host '📁 Распаковка обновления...'

if ($updateFile -match '\.zip$') {{
    Expand-Archive -Path $updateFile -DestinationPath $tempPath -Force
    $sourceDir = $tempPath
}} else {{
    $sourceDir = $tempPath
    Copy-Item $updateFile $sourceDir -Force
}}

Write-Host '🔄 Замена файлов...'
Copy-Item -Path $sourceDir\* -Destination $appDir -Recurse -Force

Write-Host '✅ Обновление установлено!' -ForegroundColor Green
Start-Sleep -Seconds 2

Start-Process -FilePath '$appDir\WPF-Sorter-2.0.exe'

Remove-Item -Path $tempPath -Recurse -Force -ErrorAction SilentlyContinue
";

        File.WriteAllText(scriptPath, scriptContent);
        return scriptPath;
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