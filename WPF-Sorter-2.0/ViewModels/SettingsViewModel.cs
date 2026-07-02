using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Options;
using WPF_Sorter_2._0.Contracts.Services;
using WPF_Sorter_2._0.Contracts.ViewModels;
using WPF_Sorter_2._0.Models;

namespace WPF_Sorter_2._0.ViewModels;

public partial class SettingsViewModel : ObservableObject, INavigationAware
{
    private readonly AppConfig _appConfig;
    private readonly IThemeSelectorService _themeSelectorService;
    private readonly ISystemService _systemService;
    private readonly IApplicationInfoService _applicationInfoService;
    private AppTheme _theme;
    private string _versionDescription;
    private ICommand _setThemeCommand;
    private ICommand _privacyStatementCommand;

    [ObservableProperty]
    private string _applicationInfo = string.Empty;

    public AppTheme Theme
    {
        get { return _theme; }
        set { SetProperty(ref _theme, value); }
    }

    public string VersionDescription
    {
        get { return _versionDescription; }
        set { SetProperty(ref _versionDescription, value); }
    }

    public ICommand SetThemeCommand => _setThemeCommand ?? (_setThemeCommand = new RelayCommand<string>(OnSetTheme));

    public ICommand PrivacyStatementCommand => _privacyStatementCommand ?? (_privacyStatementCommand = new RelayCommand(OnPrivacyStatement));

    public SettingsViewModel(
        IOptions<AppConfig> appConfig,
        IThemeSelectorService themeSelectorService,
        ISystemService systemService,
        IApplicationInfoService applicationInfoService)
    {
        _appConfig = appConfig.Value;
        _themeSelectorService = themeSelectorService;
        _systemService = systemService;
        _applicationInfoService = applicationInfoService;

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

        System.Diagnostics.Debug.WriteLine($"VersionDescription: {VersionDescription}");
        System.Diagnostics.Debug.WriteLine($"ApplicationInfo: {ApplicationInfo}");
        System.Diagnostics.Debug.WriteLine("=== END ===");
    }

    public void OnNavigatedFrom()
    {
    }

    private void OnSetTheme(string themeName)
    {
        var theme = (AppTheme)Enum.Parse(typeof(AppTheme), themeName);
        _themeSelectorService.SetTheme(theme);
    }

    private void OnPrivacyStatement()
        => _systemService.OpenInWebBrowser(_appConfig.PrivacyStatement);
}