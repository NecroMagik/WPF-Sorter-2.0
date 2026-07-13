using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MahApps.Metro.Controls;
using System.Collections.ObjectModel;
using System.Windows.Input;
using WPF_Sorter_2._0.Contracts.Services;
using WPF_Sorter_2._0.Core.Models;
using WPF_Sorter_2._0.Services;

namespace WPF_Sorter_2._0.ViewModels;

public partial class ShellViewModel : ObservableObject, IDisposable
{
    private readonly INavigationService _navigationService;
    private readonly SortBackgroundService _sortService;
    private bool _disposed = false;

    private HamburgerMenuItem _selectedMenuItem;
    private HamburgerMenuItem _selectedOptionsMenuItem;
    private RelayCommand _goBackCommand;
    private ICommand _menuItemInvokedCommand;
    private ICommand _optionsMenuItemInvokedCommand;
    private ICommand _loadedCommand;
    private ICommand _unloadedCommand;

    // 👇 Храним делегаты для отписки
    private readonly EventHandler<SortProgress> _progressHandler;
    private readonly EventHandler _startedHandler;
    private readonly EventHandler<List<FileOperationResult>> _completedHandler;
    private readonly EventHandler<string> _failedHandler;
    private readonly EventHandler _cancelledHandler;
    private readonly EventHandler<string> _navigatedHandler;

    [ObservableProperty]
    private string _sortStatus = "Сортировка...";

    [ObservableProperty]
    private int _sortProgress = 0;

    [ObservableProperty]
    private bool _isSorting = false;

    public HamburgerMenuItem SelectedMenuItem
    {
        get { return _selectedMenuItem; }
        set { SetProperty(ref _selectedMenuItem, value); }
    }

    public HamburgerMenuItem SelectedOptionsMenuItem
    {
        get { return _selectedOptionsMenuItem; }
        set { SetProperty(ref _selectedOptionsMenuItem, value); }
    }

    public ObservableCollection<HamburgerMenuItem> MenuItems { get; } = new ObservableCollection<HamburgerMenuItem>()
    {
        new HamburgerMenuGlyphItem() { Label = "Главная", Glyph = "\uE721", TargetPageType = typeof(MainViewModel) },
        new HamburgerMenuGlyphItem() { Label = "Категории", Glyph = "\uEA86", TargetPageType = typeof(SortSetViewModel) },
    };

    public ObservableCollection<HamburgerMenuItem> OptionMenuItems { get; } = new ObservableCollection<HamburgerMenuItem>()
    {
        new HamburgerMenuGlyphItem() { Label = "Настройки", Glyph = "\uE713", TargetPageType = typeof(SettingsViewModel) }
    };

    public RelayCommand GoBackCommand => _goBackCommand ?? (_goBackCommand = new RelayCommand(OnGoBack, CanGoBack));

    public ICommand MenuItemInvokedCommand => _menuItemInvokedCommand ?? (_menuItemInvokedCommand = new RelayCommand(OnMenuItemInvoked));

    public ICommand OptionsMenuItemInvokedCommand => _optionsMenuItemInvokedCommand ?? (_optionsMenuItemInvokedCommand = new RelayCommand(OnOptionsMenuItemInvoked));

    public ICommand LoadedCommand => _loadedCommand ?? (_loadedCommand = new RelayCommand(OnLoaded));

    public ICommand UnloadedCommand => _unloadedCommand ?? (_unloadedCommand = new RelayCommand(OnUnloaded));

    [RelayCommand]
    private void CancelSorting()
    {
        try
        {
            _sortService?.CancelSorting();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ CancelSorting error: {ex.Message}");
        }
    }

    public ShellViewModel(INavigationService navigationService, SortBackgroundService sortService)
    {
        _navigationService = navigationService;
        _sortService = sortService;

        // 👇 Сохраняем делегаты
        _progressHandler = OnSortProgressUpdated;
        _startedHandler = OnSortStarted;
        _completedHandler = OnSortCompleted;
        _failedHandler = OnSortFailed;
        _cancelledHandler = OnSortCancelled;
        _navigatedHandler = OnNavigated;

        // Подписываемся на события сортировки
        if (_sortService != null)
        {
            _sortService.ProgressUpdated += _progressHandler;
            _sortService.SortStarted += _startedHandler;
            _sortService.SortCompleted += _completedHandler;
            _sortService.SortFailed += _failedHandler;
            _sortService.SortCancelled += _cancelledHandler;

            // Восстанавливаем состояние
            if (_sortService.IsSorting && _sortService.CurrentProgress != null)
            {
                IsSorting = true;
                UpdateSortProgress(_sortService.CurrentProgress);
            }
        }
    }

    private void OnSortStarted(object? sender, EventArgs e)
    {
        try
        {
            IsSorting = true;
            SortProgress = 0;
            SortStatus = "🔍 Поиск файлов...";
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
                UpdateSortProgress(progress);
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
            SortProgress = 100;
            SortStatus = $"✅ Готово! Обработано {results.Count(r => r.Success)} файлов";
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
            SortStatus = $"❌ Ошибка: {errorMessage}";
            SortProgress = 0;
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
            SortStatus = "⏹️ Сортировка отменена";
            SortProgress = 0;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ OnSortCancelled error: {ex.Message}");
        }
    }

    private void UpdateSortProgress(SortProgress progress)
    {
        try
        {
            SortProgress = (int)Math.Min(100, progress.ProgressPercentage);
            SortStatus = progress.CurrentStatus ?? "Обработка...";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ UpdateSortProgress error: {ex.Message}");
        }
    }

    private void OnLoaded()
    {
        try
        {
            if (_navigationService != null)
            {
                _navigationService.Navigated += _navigatedHandler;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ OnLoaded error: {ex.Message}");
        }
    }

    private void OnUnloaded()
    {
        try
        {
            if (_navigationService != null)
            {
                _navigationService.Navigated -= _navigatedHandler;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ OnUnloaded error: {ex.Message}");
        }
    }

    private bool CanGoBack()
        => _navigationService?.CanGoBack ?? false;

    private void OnGoBack()
        => _navigationService?.GoBack();

    private void OnMenuItemInvoked()
        => NavigateTo(SelectedMenuItem?.TargetPageType);

    private void OnOptionsMenuItemInvoked()
        => NavigateTo(SelectedOptionsMenuItem?.TargetPageType);

    private void NavigateTo(Type? targetViewModel)
    {
        try
        {
            if (targetViewModel != null && _navigationService != null)
            {
                _navigationService.NavigateTo(targetViewModel.FullName);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ NavigateTo error: {ex.Message}");
        }
    }

    private void OnNavigated(object sender, string viewModelName)
    {
        try
        {
            var item = MenuItems
                        .OfType<HamburgerMenuItem>()
                        .FirstOrDefault(i => viewModelName == i.TargetPageType?.FullName);
            if (item != null)
            {
                SelectedMenuItem = item;
            }
            else
            {
                SelectedOptionsMenuItem = OptionMenuItems
                        .OfType<HamburgerMenuItem>()
                        .FirstOrDefault(i => viewModelName == i.TargetPageType?.FullName);
            }

            GoBackCommand.NotifyCanExecuteChanged();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ OnNavigated error: {ex.Message}");
        }
    }

    // 👇 IDisposable для отписки
    public void Dispose()
    {
        if (!_disposed)
        {
            if (_sortService != null)
            {
                _sortService.ProgressUpdated -= _progressHandler;
                _sortService.SortStarted -= _startedHandler;
                _sortService.SortCompleted -= _completedHandler;
                _sortService.SortFailed -= _failedHandler;
                _sortService.SortCancelled -= _cancelledHandler;
            }

            if (_navigationService != null)
            {
                _navigationService.Navigated -= _navigatedHandler;
            }

            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}