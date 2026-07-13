#nullable enable

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Options;
using System.Windows;
using System.Windows.Input;
using WPF_Sorter_2._0.Contracts.Services;
using WPF_Sorter_2._0.Contracts.ViewModels;
using WPF_Sorter_2._0.Core.Models;  // 👈 UpdateInfo ИЗ Core
using WPF_Sorter_2._0.Models;
using WPF_Sorter_2._0.Services;
using WPF_Sorter_2._0.Views;

namespace WPF_Sorter_2._0.ViewModels;

public partial class SettingsViewModel : ObservableObject, INavigationAware
{
    private readonly AppConfig _appConfig;
    private readonly IThemeSelectorService _themeSelectorService;
    private readonly ISystemService _systemService;
    private readonly IApplicationInfoService _applicationInfoService;
    private readonly UpdateService _updateService;
    private AppTheme _theme;
    private string _versionDescription = string.Empty;
    private ICommand? _setThemeCommand;
    private ICommand? _privacyStatementCommand;

    [ObservableProperty]
    private string _applicationInfo = string.Empty;

    [ObservableProperty]
    private bool _isCheckingForUpdates = false;

    [ObservableProperty]
    private string _updateStatus = string.Empty;

    public AppTheme Theme
    {
        get => _theme;
        set => SetProperty(ref _theme, value);
    }

    public string VersionDescription
    {
        get => _versionDescription;
        set => SetProperty(ref _versionDescription, value);
    }

    public ICommand SetThemeCommand => _setThemeCommand ??= new RelayCommand<string>(OnSetTheme);
    public ICommand PrivacyStatementCommand => _privacyStatementCommand ??= new RelayCommand(OnPrivacyStatement);
    public ICommand CheckForUpdatesCommand => new RelayCommand(async () => await CheckForUpdatesAsync());

    public SettingsViewModel(
        IOptions<AppConfig> appConfig,
        IThemeSelectorService themeSelectorService,
        ISystemService systemService,
        IApplicationInfoService applicationInfoService,
        UpdateService updateService)
    {
        _appConfig = appConfig.Value;
        _themeSelectorService = themeSelectorService;
        _systemService = systemService;
        _applicationInfoService = applicationInfoService;
        _updateService = updateService;

        System.Diagnostics.Debug.WriteLine("=== SettingsViewModel CONSTRUCTOR CALLED ===");
    }

    public void OnNavigatedTo(object parameter)
    {
        System.Diagnostics.Debug.WriteLine("=== SettingsViewModel.OnNavigatedTo CALLED ===");

        var version = _applicationInfoService.GetVersion();
        var productName = _applicationInfoService.GetProductName();

        VersionDescription = $"{productName} - {version}";
        Theme = _themeSelectorService.GetCurrentTheme();

        ApplicationInfo = $"Версия: {version}\n" +
                         $"Разработчик: ZeN\n" +
                         $"Платформа: .NET 8.0\n" +
                         $"Репозиторий: github.com/NecroMagik/WPF-Sorter-2.0";

        UpdateStatus = string.Empty;
    }

    public void OnNavigatedFrom() { }

    private async Task CheckForUpdatesAsync()
    {
        if (IsCheckingForUpdates) return;

        IsCheckingForUpdates = true;
        UpdateStatus = "🔍 Проверка обновлений...";

        try
        {
            _updateService.ResetToastFlag();
            var updateInfo = await _updateService.CheckForUpdatesAsync(showToastIfAvailable: false);

            if (updateInfo == null)
            {
                UpdateStatus = "✅ У вас последняя версия!";
                return;
            }

            var dialog = new UpdateDialog(updateInfo, _applicationInfoService.GetVersion(), _updateService);
            dialog.Owner = Application.Current.MainWindow;

            var result = dialog.ShowDialog();

            if (result == true && dialog.IsInstallConfirmed)
            {
                await DownloadAndInstallUpdateAsync(updateInfo);
            }
            else
            {
                UpdateStatus = "⏸️ Обновление отложено";
            }
        }
        catch (Exception ex)
        {
            UpdateStatus = $"❌ Ошибка: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"❌ Ошибка проверки обновлений: {ex.Message}");
        }
        finally
        {
            IsCheckingForUpdates = false;
        }
    }

    private async Task DownloadAndInstallUpdateAsync(UpdateInfo updateInfo)
    {
        UpdateStatus = "⬇️ Скачивание обновления...";

        var progress = new Progress<int>(p =>
        {
            UpdateStatus = $"⬇️ Скачивание: {p}%";
        });

        try
        {
            var filePath = await _updateService.DownloadUpdateAsync(updateInfo, progress);
            UpdateStatus = "✅ Загрузка завершена! Установка...";
            _updateService.InstallUpdate(filePath);
        }
        catch (Exception ex)
        {
            UpdateStatus = $"❌ Ошибка скачивания: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"❌ Ошибка скачивания: {ex.Message}");
        }
    }

    private void OnSetTheme(string themeName)
    {
        var theme = (AppTheme)Enum.Parse(typeof(AppTheme), themeName);
        _themeSelectorService.SetTheme(theme);
    }

    private void OnPrivacyStatement()
        => _systemService.OpenInWebBrowser(_appConfig.PrivacyStatement);
}