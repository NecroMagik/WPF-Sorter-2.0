using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ookii.Dialogs.Wpf;
using System.IO;
using System.Windows;
using WPF_Sorter_2._0.Contracts.ViewModels;
using WPF_Sorter_2._0.Core.Models;
using WPF_Sorter_2._0.Services;

namespace WPF_Sorter_2._0.ViewModels;

public partial class MainViewModel : ObservableObject, INavigationAware, IDisposable
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
    private readonly SettingsService _settingsService;
    private bool _disposed = false;

    // 👇 Храним слабые ссылки на обработчики для возможности отписки
    private readonly EventHandler<SortProgress> _progressHandler;
    private readonly EventHandler<List<FileOperationResult>> _completedHandler;
    private readonly EventHandler<string> _failedHandler;
    private readonly EventHandler _startedHandler;
    private readonly EventHandler _cancelledHandler;

    public MainViewModel(SortBackgroundService sortService, SettingsService settingsService)
    {
        _sortService = sortService;
        _settingsService = settingsService;

        // 👇 Сохраняем делегаты для отписки
        _progressHandler = OnSortProgressUpdated;
        _completedHandler = OnSortCompleted;
        _failedHandler = OnSortFailed;
        _startedHandler = OnSortStarted;
        _cancelledHandler = OnSortCancelled;

        // Подписываемся на события сортировки
        _sortService.ProgressUpdated += _progressHandler;
        _sortService.SortCompleted += _completedHandler;
        _sortService.SortFailed += _failedHandler;
        _sortService.SortStarted += _startedHandler;
        _sortService.SortCancelled += _cancelledHandler;

        // Восстанавливаем состояние, если сортировка уже идёт
        RestoreSortingState();

        // Загружаем последнюю папку
        LoadLastFolder();
    }

    private void LoadLastFolder()
    {
        try
        {
            var settings = _settingsService.Settings;
            if (!string.IsNullOrEmpty(settings.LastSelectedFolder) && Directory.Exists(settings.LastSelectedFolder))
            {
                SelectedFolder = settings.LastSelectedFolder;
                IncludeSubfolders = settings.IncludeSubfolders;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ Error loading last folder: {ex.Message}");
        }
    }

    public void OnNavigatedTo(object parameter)
    {
        RestoreSortingState();
    }

    public void OnNavigatedFrom()
    {
        // Ничего не делаем, но оставляем для INavigationAware
    }

    private void RestoreSortingState()
    {
        try
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
                var successCount = results.Count(r => r.Success);
                var hashPassed = results.Count(r => r.HashVerified);
                var hashFailed = results.Count(r => !r.HashVerified && r.Success);

                StatusText = $"✅ Готово! Обработано {successCount} файлов";
                ProcessedFilesText = $"Обработано: {successCount} из {results.Count}";
                FilesFoundText = $"ХЭШ: ✅{hashPassed} ❌{hashFailed}";
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
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ Error restoring state: {ex.Message}");
            IsSorting = false;
            ProgressVisibility = Visibility.Collapsed;
            StatusText = "Готов к работе";
        }
    }

    [RelayCommand]
    private void BrowseFolder()
    {
        try
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
        catch (Exception ex)
        {
            StatusText = $"❌ Ошибка выбора папки: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"❌ BrowseFolder error: {ex.Message}");
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

        // Проверяем, есть ли файлы
        try
        {
            var searchOption = IncludeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var files = Directory.GetFiles(SelectedFolder, "*.*", searchOption);
            if (files.Length == 0)
            {
                StatusText = "⚠️ В выбранной папке нет файлов";
                return;
            }
        }
        catch (Exception ex)
        {
            StatusText = $"⚠️ Ошибка доступа к папке: {ex.Message}";
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
        try
        {
            IsSorting = true;
            ProgressVisibility = Visibility.Visible;
            ProgressValue = 0;
            StatusText = "🔍 Поиск файлов...";
            ProcessedFilesText = "Обработано: 0";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ OnSortStarted error: {ex.Message}");
        }
    }

    private void OnSortProgressUpdated(object? sender, SortProgress progress)
    {
        try
        {
            if (progress != null)
            {
                UpdateUIFromProgress(progress);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ OnSortProgressUpdated error: {ex.Message}");
        }
    }

    private void OnSortCompleted(object? sender, List<FileOperationResult> results)
    {
        try
        {
            IsSorting = false;
            ProgressValue = 100;
            var successCount = results.Count(r => r.Success);
            var hashPassed = results.Count(r => r.HashVerified);
            var hashFailed = results.Count(r => !r.HashVerified && r.Success);

            StatusText = $"✅ Готово! Обработано {successCount} файлов";
            ProcessedFilesText = $"Обработано: {successCount} из {results.Count}";
            FilesFoundText = $"ХЭШ: ✅{hashPassed} ❌{hashFailed}";

            // Скрываем прогресс через 3 секунды
            _ = Task.Delay(3000).ContinueWith(_ =>
            {
                try
                {
                    ProgressVisibility = Visibility.Collapsed;
                }
                catch { }
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ OnSortCompleted error: {ex.Message}");
        }
    }

    private void OnSortFailed(object? sender, string errorMessage)
    {
        try
        {
            IsSorting = false;
            StatusText = $"❌ Ошибка: {errorMessage}";
            ProgressVisibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ OnSortFailed error: {ex.Message}");
        }
    }

    private void OnSortCancelled(object? sender, EventArgs e)
    {
        try
        {
            IsSorting = false;
            StatusText = "⏹️ Сортировка отменена";
            ProgressVisibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ OnSortCancelled error: {ex.Message}");
        }
    }

    private void UpdateUIFromProgress(SortProgress progress)
    {
        try
        {
            ProgressValue = (int)Math.Min(100, progress.ProgressPercentage);
            StatusText = progress.CurrentStatus ?? "Обработка...";
            ProcessedFilesText = $"Обработано: {progress.ProcessedFiles} из {progress.TotalFiles}";
            FilesFoundText = $"ХЭШ: ✅{progress.HashCheckPassed} ❌{progress.HashCheckFailed}";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ UpdateUIFromProgress error: {ex.Message}");
        }
    }

    private void UpdateFileInfo()
    {
        try
        {
            if (string.IsNullOrEmpty(SelectedFolder) || !Directory.Exists(SelectedFolder))
            {
                FilesFoundText = "Файлов найдено: 0";
                return;
            }

            var searchOption = IncludeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var count = Directory.GetFiles(SelectedFolder, "*.*", searchOption).Length;
            FilesFoundText = $"Файлов найдено: {count}";
        }
        catch (UnauthorizedAccessException)
        {
            FilesFoundText = "Файлов найдено: 0 (нет доступа)";
        }
        catch (Exception ex)
        {
            FilesFoundText = "Файлов найдено: 0";
            System.Diagnostics.Debug.WriteLine($"❌ UpdateFileInfo error: {ex.Message}");
        }
    }

    private List<FileCategory> GetEnabledCategories()
    {
        try
        {
            var settings = _settingsService.Settings;
            if (settings.Categories != null)
            {
                return settings.Categories.Where(c => c.IsEnabled).ToList();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ GetEnabledCategories error: {ex.Message}");
        }

        return new List<FileCategory>();
    }

    partial void OnIncludeSubfoldersChanged(bool value)
    {
        try
        {
            UpdateFileInfo();

            // Сохраняем настройку
            var settings = _settingsService.Settings;
            settings.IncludeSubfolders = value;
            _settingsService.SaveSettings();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ OnIncludeSubfoldersChanged error: {ex.Message}");
        }
    }

    partial void OnSelectedFolderChanged(string value)
    {
        UpdateFileInfo();
    }

    // 👇 IDisposable для отписки от событий
    public void Dispose()
    {
        if (!_disposed)
        {
            // Отписываемся от всех событий
            if (_sortService != null)
            {
                _sortService.ProgressUpdated -= _progressHandler;
                _sortService.SortCompleted -= _completedHandler;
                _sortService.SortFailed -= _failedHandler;
                _sortService.SortStarted -= _startedHandler;
                _sortService.SortCancelled -= _cancelledHandler;
            }

            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}