using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ookii.Dialogs.Wpf;
using System.IO;
using System.Windows;
using WPF_Sorter_2._0.Contracts.ViewModels;
using WPF_Sorter_2._0.Core.Models;
using WPF_Sorter_2._0.Services;

namespace WPF_Sorter_2._0.ViewModels;

public partial class MainViewModel : ObservableObject, INavigationAware
{
    [ObservableProperty]
    private string _selectedFolder = string.Empty;

    [ObservableProperty]
    private bool _includeSubfolders = true;

    [ObservableProperty]
    private bool _isSorting = false;

    [ObservableProperty]
    private int _progressValue = 0;

    [ObservableProperty]
    private string _statusText = "Готов к работе";

    [ObservableProperty]
    private string _filesFoundText = "Файлов найдено: 0";

    [ObservableProperty]
    private string _processedFilesText = "Обработано: 0";

    [ObservableProperty]
    private Visibility _progressVisibility = Visibility.Collapsed;

    private readonly SortBackgroundService _sortService;
    private readonly SettingsService _settingsService; // 👈 Добавлено!

    public MainViewModel(SortBackgroundService sortService, SettingsService settingsService)
    {
        _sortService = sortService;
        _settingsService = settingsService;

        // Подписываемся на события сортировки
        _sortService.ProgressUpdated += OnSortProgressUpdated;
        _sortService.SortCompleted += OnSortCompleted;
        _sortService.SortFailed += OnSortFailed;
        _sortService.SortStarted += OnSortStarted;
        _sortService.SortCancelled += OnSortCancelled;

        // Восстанавливаем состояние, если сортировка уже идёт
        RestoreSortingState();

        // Загружаем последнюю папку
        var settings = _settingsService.Settings;
        if (!string.IsNullOrEmpty(settings.LastSelectedFolder) && Directory.Exists(settings.LastSelectedFolder))
        {
            SelectedFolder = settings.LastSelectedFolder;
            IncludeSubfolders = settings.IncludeSubfolders;
        }
    }

    // 👇 Реализация INavigationAware
    public void OnNavigatedTo(object parameter)
    {
        RestoreSortingState();
    }

    public void OnNavigatedFrom()
    {
        // Ничего не делаем
    }

    private void RestoreSortingState()
    {
        if (_sortService.IsSorting && _sortService.CurrentProgress != null)
        {
            IsSorting = true;
            ProgressVisibility = Visibility.Visible;
            UpdateUIFromProgress(_sortService.CurrentProgress);

            if (!string.IsNullOrEmpty(_sortService.CurrentSourceFolder))
            {
                SelectedFolder = _sortService.CurrentSourceFolder;
                IncludeSubfolders = _sortService.CurrentIncludeSubfolders;
            }
        }
        else if (_sortService.LastResults != null && _sortService.LastResults.Count > 0)
        {
            var results = _sortService.LastResults;
            StatusText = $"✅ Готово! Обработано {results.Count(r => r.Success)} файлов";
            ProcessedFilesText = $"Обработано: {results.Count(r => r.Success)} из {results.Count}";
            FilesFoundText = $"ХЭШ: ✅{results.Count(r => r.HashVerified)} ❌{results.Count(r => !r.HashVerified && r.Success)}";
            ProgressValue = 100;
            ProgressVisibility = Visibility.Collapsed;
            IsSorting = false;
        }
        else
        {
            IsSorting = false;
            ProgressVisibility = Visibility.Collapsed;
            StatusText = "Готов к работе";
            ProcessedFilesText = "Обработано: 0";
            FilesFoundText = "Файлов найдено: 0";
            ProgressValue = 0;
        }
    }

    // 👇 ТОЛЬКО ОДИН BrowseFolder
    [RelayCommand]
    private void BrowseFolder()
    {
        var dialog = new VistaFolderBrowserDialog
        {
            Description = "Выберите папку для сортировки",
            UseDescriptionForTitle = true,
            SelectedPath = SelectedFolder
        };

        if (dialog.ShowDialog() == true)
        {
            SelectedFolder = dialog.SelectedPath;
            UpdateFileInfo();

            // Сохраняем последнюю папку
            var settings = _settingsService.Settings;
            settings.LastSelectedFolder = SelectedFolder;
            settings.IncludeSubfolders = IncludeSubfolders;
            _settingsService.SaveSettings();
        }
    }

    [RelayCommand]
    private async Task StartSortingAsync()
    {
        if (string.IsNullOrEmpty(SelectedFolder) || !Directory.Exists(SelectedFolder))
        {
            StatusText = "⚠️ Пожалуйста, выберите существующую папку";
            return;
        }

        var categories = GetEnabledCategories();
        if (categories.Count == 0)
        {
            StatusText = "⚠️ Нет активных категорий для сортировки. Перейдите в настройки.";
            return;
        }

        await _sortService.StartSortingAsync(SelectedFolder, IncludeSubfolders);
    }

    [RelayCommand]
    private void CancelSorting()
    {
        _sortService.CancelSorting();
        StatusText = "⏹️ Отмена сортировки...";
    }

    private void OnSortStarted(object? sender, EventArgs e)
    {
        IsSorting = true;
        ProgressVisibility = Visibility.Visible;
        ProgressValue = 0;
        StatusText = "🔍 Поиск файлов...";
        ProcessedFilesText = "Обработано: 0";
    }

    private void OnSortProgressUpdated(object? sender, SortProgress progress)
    {
        UpdateUIFromProgress(progress);
    }

    private void OnSortCompleted(object? sender, List<FileOperationResult> results)
    {
        IsSorting = false;
        ProgressValue = 100;
        StatusText = $"✅ Готово! Обработано {results.Count(r => r.Success)} файлов";
        ProcessedFilesText = $"Обработано: {results.Count(r => r.Success)} из {results.Count}";
        FilesFoundText = $"ХЭШ: ✅{results.Count(r => r.HashVerified)} ❌{results.Count(r => !r.HashVerified && r.Success)}";

        _ = Task.Delay(3000).ContinueWith(_ =>
        {
            ProgressVisibility = Visibility.Collapsed;
        });
    }

    private void OnSortFailed(object? sender, string errorMessage)
    {
        IsSorting = false;
        StatusText = $"❌ Ошибка: {errorMessage}";
        ProgressVisibility = Visibility.Collapsed;
    }

    private void OnSortCancelled(object? sender, EventArgs e)
    {
        IsSorting = false;
        StatusText = "⏹️ Сортировка отменена";
        ProgressVisibility = Visibility.Collapsed;
    }

    private void UpdateUIFromProgress(SortProgress progress)
    {
        ProgressValue = (int)progress.ProgressPercentage;
        StatusText = progress.CurrentStatus;
        ProcessedFilesText = $"Обработано: {progress.ProcessedFiles} из {progress.TotalFiles}";
        FilesFoundText = $"ХЭШ: ✅{progress.HashCheckPassed} ❌{progress.HashCheckFailed}";
    }

    private void UpdateFileInfo()
    {
        if (string.IsNullOrEmpty(SelectedFolder) || !Directory.Exists(SelectedFolder))
        {
            FilesFoundText = "Файлов найдено: 0";
            return;
        }

        try
        {
            var searchOption = IncludeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var count = Directory.GetFiles(SelectedFolder, "*.*", searchOption).Length;
            FilesFoundText = $"Файлов найдено: {count}";
        }
        catch
        {
            FilesFoundText = "Файлов найдено: 0";
        }
    }

    private List<Core.Models.FileCategory> GetEnabledCategories()
    {
        try
        {
            var app = Application.Current as App;
            if (app != null)
            {
                var sortSetVM = app.GetService<SortSetViewModel>();
                if (sortSetVM != null)
                {
                    return sortSetVM.Categories.Where(c => c.IsEnabled).ToList();
                }
            }
        }
        catch { }

        return new List<Core.Models.FileCategory>();
    }

    // 👇 ТОЛЬКО ОДИН OnIncludeSubfoldersChanged
    partial void OnIncludeSubfoldersChanged(bool value)
    {
        UpdateFileInfo();

        // Сохраняем настройку
        var settings = _settingsService.Settings;
        settings.IncludeSubfolders = value;
        _settingsService.SaveSettings();
    }

    partial void OnSelectedFolderChanged(string value)
    {
        UpdateFileInfo();
    }
}