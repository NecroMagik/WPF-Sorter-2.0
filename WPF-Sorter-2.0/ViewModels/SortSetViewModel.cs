using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ookii.Dialogs.Wpf;
using System.Collections.ObjectModel;
using System.IO;
using WPF_Sorter_2._0.Contracts.ViewModels;
using WPF_Sorter_2._0.Core.Models;
using WPF_Sorter_2._0.Services;

namespace WPF_Sorter_2._0.ViewModels;

public partial class SortSetViewModel : ObservableObject, INavigationAware
{
    [ObservableProperty]
    private ObservableCollection<FileCategory> _categories = new();

    [ObservableProperty]
    private string _libraryDetectionStatus = string.Empty;

    [ObservableProperty]
    private bool _isLibraryStatusVisible = false;

    private readonly SettingsService _settingsService;
    private bool _isLoading = false;

    public SortSetViewModel(SettingsService settingsService)
    {
        _settingsService = settingsService;
        LoadSettings();
    }

    private void LoadSettings()
    {
        _isLoading = true;

        try
        {
            var settings = _settingsService.Settings;

            if (settings.Categories != null && settings.Categories.Count > 0)
            {
                Categories = new ObservableCollection<FileCategory>(settings.Categories);
            }
            else
            {
                LoadDefaultCategories();
            }
        }
        catch
        {
            LoadDefaultCategories();
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void LoadDefaultCategories()
    {
        Categories = new ObservableCollection<FileCategory>
        {
            new()
            {
                Name = "Фото",
                IconGlyph = "\uE114",
                Extensions = new List<string> { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff", ".webp" },
                DefaultPath = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                CustomPath = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                IsEnabled = true
            },
            new()
            {
                Name = "Видео",
                IconGlyph = "\uE116",
                Extensions = new List<string> { ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv", ".webm" },
                DefaultPath = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
                CustomPath = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
                IsEnabled = true
            },
            new()
            {
                Name = "Музыка",
                IconGlyph = "\uE7F6",
                Extensions = new List<string> { ".mp3", ".wav", ".flac", ".aac", ".ogg", ".wma" },
                DefaultPath = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
                CustomPath = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
                IsEnabled = true
            },
            new()
            {
                Name = "Документы",
                IconGlyph = "\uE197",
                Extensions = new List<string> { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".rtf" },
                DefaultPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                CustomPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                IsEnabled = true
            }
        };
    }

    public void OnNavigatedTo(object parameter)
    {
    }

    public void OnNavigatedFrom()
    {
        SaveSettings();
    }

    private void SaveSettings()
    {
        if (_isLoading) return;

        try
        {
            var settings = _settingsService.Settings;
            settings.Categories = Categories.ToList();
            _settingsService.SaveSettings();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ Error saving settings: {ex.Message}");
        }
    }

    [RelayCommand]
    private void BrowsePath(FileCategory category)
    {
        var dialog = new VistaFolderBrowserDialog
        {
            Description = $"Выберите папку для категории '{category.Name}'",
            UseDescriptionForTitle = true,
            SelectedPath = category.CustomPath
        };

        if (dialog.ShowDialog() == true)
        {
            category.CustomPath = dialog.SelectedPath;
            SaveSettings();
        }
    }

    [RelayCommand]
    private void ResetToDefault(FileCategory category)
    {
        category.CustomPath = category.DefaultPath;
        SaveSettings();
    }

    [RelayCommand]
    private async Task AutoDetectLibrariesAsync()
    {
        IsLibraryStatusVisible = true;
        LibraryDetectionStatus = "Поиск библиотек Windows...";

        await Task.Run(() =>
        {
            try
            {
                var libraryPaths = GetWindowsLibraries();

                foreach (var category in Categories)
                {
                    var matchingLib = libraryPaths.FirstOrDefault(l =>
                        l.Name.Contains(category.Name, StringComparison.OrdinalIgnoreCase) ||
                        category.Name.Contains(l.Name, StringComparison.OrdinalIgnoreCase));

                    if (!string.IsNullOrEmpty(matchingLib.Path))
                    {
                        category.CustomPath = matchingLib.Path;
                    }
                }

                LibraryDetectionStatus = $"✅ Найдено {libraryPaths.Count} библиотек. Пути обновлены.";
                SaveSettings();
            }
            catch (Exception ex)
            {
                LibraryDetectionStatus = $"⚠️ Не удалось найти библиотеки: {ex.Message}";
            }
        });
    }

    private List<(string Name, string Path)> GetWindowsLibraries()
    {
        var libraries = new List<(string Name, string Path)>();

        var defaultLibs = new Dictionary<string, Environment.SpecialFolder>
        {
            { "Documents", Environment.SpecialFolder.MyDocuments },
            { "Music", Environment.SpecialFolder.MyMusic },
            { "Pictures", Environment.SpecialFolder.MyPictures },
            { "Videos", Environment.SpecialFolder.MyVideos },
            { "Downloads", Environment.SpecialFolder.UserProfile }
        };

        foreach (var lib in defaultLibs)
        {
            var path = Environment.GetFolderPath(lib.Value);
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            {
                libraries.Add((lib.Key, path));
            }
        }

        var oneDrivePath = Environment.GetEnvironmentVariable("OneDrive");
        if (!string.IsNullOrEmpty(oneDrivePath) && Directory.Exists(oneDrivePath))
        {
            libraries.Add(("OneDrive", oneDrivePath));

            var subFolders = new[] { "Documents", "Pictures", "Music", "Videos" };
            foreach (var folder in subFolders)
            {
                var subPath = Path.Combine(oneDrivePath, folder);
                if (Directory.Exists(subPath))
                {
                    libraries.Add(($"{folder} (OneDrive)", subPath));
                }
            }
        }

        return libraries;
    }
}