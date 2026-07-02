using System.IO;
using System.Text.Json;
using WPF_Sorter_2._0.Core.Models;

namespace WPF_Sorter_2._0.Services;

public class SettingsService
{
    private readonly string _settingsFilePath;
    private AppSettings _settings;
    private readonly object _lock = new object();

    public SettingsService()
    {
        // 👇 Новый путь: C:\Users\[USER]\ZeN\Sorter
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "ZeN",
            "Sorter");

        if (!Directory.Exists(appDataPath))
        {
            Directory.CreateDirectory(appDataPath);
            System.Diagnostics.Debug.WriteLine($"📁 Created settings directory: {appDataPath}");
        }

        _settingsFilePath = Path.Combine(appDataPath, "settings.json");
        System.Diagnostics.Debug.WriteLine($"📁 Settings path: {_settingsFilePath}");
    }

    public AppSettings Settings
    {
        get
        {
            if (_settings == null)
            {
                _settings = LoadSettings();
            }
            return _settings;
        }
    }

    public async Task SaveSettingsAsync()
    {
        await Task.Run(() =>
        {
            lock (_lock)
            {
                try
                {
                    var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });
                    File.WriteAllText(_settingsFilePath, json);
                    System.Diagnostics.Debug.WriteLine($"✅ Settings saved: {_settingsFilePath}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"❌ Error saving settings: {ex.Message}");
                }
            }
        });
    }

    public void SaveSettings()
    {
        lock (_lock)
        {
            try
            {
                var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(_settingsFilePath, json);
                System.Diagnostics.Debug.WriteLine($"✅ Settings saved: {_settingsFilePath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error saving settings: {ex.Message}");
            }
        }
    }

    private AppSettings LoadSettings()
    {
        try
        {
            if (File.Exists(_settingsFilePath))
            {
                var json = File.ReadAllText(_settingsFilePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                if (settings != null)
                {
                    System.Diagnostics.Debug.WriteLine($"✅ Settings loaded: {_settingsFilePath}");
                    return settings;
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"ℹ️ Settings file not found, creating default: {_settingsFilePath}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ Error loading settings: {ex.Message}");
        }

        // Возвращаем настройки по умолчанию
        return new AppSettings();
    }

    public void ResetSettings()
    {
        lock (_lock)
        {
            _settings = new AppSettings();
            SaveSettings();
        }
    }

    public string GetSettingsPath()
    {
        return _settingsFilePath;
    }

    public string GetSettingsDirectory()
    {
        return Path.GetDirectoryName(_settingsFilePath) ?? string.Empty;
    }
}