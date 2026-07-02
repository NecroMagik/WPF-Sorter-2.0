using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MahApps.Metro.Controls;
using System.Collections.ObjectModel;
using System.Windows.Input;
using WPF_Sorter_2._0.Contracts.Services;
using WPF_Sorter_2._0.Core.Models;
using WPF_Sorter_2._0.Services;

namespace WPF_Sorter_2._0.ViewModels;

public partial class ShellViewModel : ObservableObject
{
    private readonly INavigationService _navigationService;
    private readonly SortBackgroundService _sortService;

    private HamburgerMenuItem _selectedMenuItem;
    private HamburgerMenuItem _selectedOptionsMenuItem;
    private RelayCommand _goBackCommand;
    private ICommand _menuItemInvokedCommand;
    private ICommand _optionsMenuItemInvokedCommand;
    private ICommand _loadedCommand;
    private ICommand _unloadedCommand;

    // 👇 Свойства для статус-бара
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

    // 👇 Команда для кнопки "Отмена"
    [RelayCommand]
    private void CancelSorting()
    {
        _sortService?.CancelSorting();
    }

    public ShellViewModel(INavigationService navigationService, SortBackgroundService sortService)
    {
        _navigationService = navigationService;
        _sortService = sortService;

        // Подписываемся на события сортировки
        if (_sortService != null)
        {
            _sortService.ProgressUpdated += OnSortProgressUpdated;
            _sortService.SortStarted += OnSortStarted;
            _sortService.SortCompleted += OnSortCompleted;
            _sortService.SortFailed += OnSortFailed;
            _sortService.SortCancelled += OnSortCancelled;

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
        IsSorting = true;
        SortProgress = 0;
        SortStatus = "🔍 Поиск файлов...";
    }

    private void OnSortProgressUpdated(object? sender, SortProgress progress)
    {
        UpdateSortProgress(progress);
    }

    private void OnSortCompleted(object? sender, List<FileOperationResult> results)
    {
        IsSorting = false;
        SortProgress = 100;
        SortStatus = $"✅ Готово! Обработано {results.Count(r => r.Success)} файлов";
    }

    private void OnSortFailed(object? sender, string errorMessage)
    {
        IsSorting = false;
        SortStatus = $"❌ Ошибка: {errorMessage}";
        SortProgress = 0;
    }

    private void OnSortCancelled(object? sender, EventArgs e)
    {
        IsSorting = false;
        SortStatus = "⏹️ Сортировка отменена";
        SortProgress = 0;
    }

    private void UpdateSortProgress(SortProgress progress)
    {
        SortProgress = (int)progress.ProgressPercentage;
        SortStatus = progress.CurrentStatus;
    }

    private void OnLoaded()
    {
        _navigationService.Navigated += OnNavigated;
    }

    private void OnUnloaded()
    {
        _navigationService.Navigated -= OnNavigated;
    }

    private bool CanGoBack()
        => _navigationService.CanGoBack;

    private void OnGoBack()
        => _navigationService.GoBack();

    private void OnMenuItemInvoked()
        => NavigateTo(SelectedMenuItem.TargetPageType);

    private void OnOptionsMenuItemInvoked()
        => NavigateTo(SelectedOptionsMenuItem.TargetPageType);

    private void NavigateTo(Type targetViewModel)
    {
        if (targetViewModel != null)
        {
            _navigationService.NavigateTo(targetViewModel.FullName);
        }
    }

    private void OnNavigated(object sender, string viewModelName)
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
}