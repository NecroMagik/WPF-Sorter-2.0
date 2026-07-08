using CommunityToolkit.WinUI.Notifications;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using WPF_Sorter_2._0.Activation;
using WPF_Sorter_2._0.Contracts.Activation;
using WPF_Sorter_2._0.Contracts.Services;
using WPF_Sorter_2._0.Contracts.Views;
using WPF_Sorter_2._0.Core.Contracts.Services;
using WPF_Sorter_2._0.Core.Services;
using WPF_Sorter_2._0.Models;
using WPF_Sorter_2._0.Services;
using WPF_Sorter_2._0.ViewModels;
using WPF_Sorter_2._0.Views;

namespace WPF_Sorter_2._0;

public partial class App : Application
{
    private IHost _host;

    public T GetService<T>()
        where T : class
        => _host.Services.GetService(typeof(T)) as T;



    public App()
    {
    }

    private async void OnStartup(object sender, StartupEventArgs e)
    {
        ToastNotificationManagerCompat.OnActivated += (toastArgs) =>
        {
            Current.Dispatcher.Invoke(async () =>
            {
                var config = GetService<IConfiguration>();
                config[ToastNotificationActivationHandler.ActivationArguments] = toastArgs.Argument;
                await _host.StartAsync();
            });
        };

        var activationArgs = new Dictionary<string, string>
        {
            { ToastNotificationActivationHandler.ActivationArguments, string.Empty }
        };
        var appLocation = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);

        _host = Host.CreateDefaultBuilder(e.Args)
                .ConfigureAppConfiguration(c =>
                {
                    c.SetBasePath(appLocation);
                    c.AddInMemoryCollection(activationArgs);
                })
                .ConfigureServices(ConfigureServices)
                .Build();

        // 👇 Загружаем системный акцент
        LoadSystemAccent();

        if (ToastNotificationManagerCompat.WasCurrentProcessToastActivated())
        {
            return;
        }

        await _host.StartAsync();
    }

    private void ConfigureServices(HostBuilderContext context, IServiceCollection services)
    {
        // App Host
        services.AddHostedService<ApplicationHostService>();

        // Activation Handlers
        services.AddSingleton<IActivationHandler, ToastNotificationActivationHandler>();

        // Core Services
        services.AddSingleton<IFileService, FileService>();
        services.AddSingleton<IHashService, HashService>();
        services.AddSingleton<IFileSystemService, FileSystemService>();
        services.AddTransient<SortEngine>();
        services.AddSingleton<SortBackgroundService>();

        // Services
        services.AddSingleton<IToastNotificationsService, ToastNotificationsService>();
        services.AddSingleton<IApplicationInfoService, ApplicationInfoService>();
        services.AddSingleton<ISystemService, SystemService>();
        services.AddSingleton<IPersistAndRestoreService, PersistAndRestoreService>();
        services.AddSingleton<IThemeSelectorService, ThemeSelectorService>();
        services.AddSingleton<IPageService, PageService>();
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<SettingsService>();

        services.AddSingleton<UpdateService>(sp =>
            new UpdateService(
                sp.GetRequiredService<IApplicationInfoService>(),
                sp.GetRequiredService<IToastNotificationsService>()
            )
        );

        // Views and ViewModels
        services.AddTransient<IShellWindow, ShellWindow>();
        services.AddTransient<ShellViewModel>();

        services.AddTransient<MainViewModel>();
        services.AddTransient<MainPage>();

        services.AddTransient<SortSetViewModel>();
        services.AddTransient<SortSetPage>();

        services.AddTransient<SettingsViewModel>();
        services.AddTransient<SettingsPage>();

        // Configuration
        services.Configure<AppConfig>(context.Configuration.GetSection(nameof(AppConfig)));
    }

    private void LoadSystemAccent()
    {
        try
        {
            // 👇 Для границ — яркий цвет
            var borderColor = SystemAccentService.GetSystemAccentColor();
            var borderBrush = new SolidColorBrush(borderColor);

            // 👇 Для интерфейса — с учётом ColorPrevalence
            var interfaceColor = SystemAccentService.GetInterfaceAccentColor();
            var interfaceBrush = new SolidColorBrush(interfaceColor);
            var interfaceLightBrush = new SolidColorBrush(Color.FromArgb(0x40, interfaceColor.R, interfaceColor.G, interfaceColor.B));

            System.Diagnostics.Debug.WriteLine($"🎨 Border accent: R={borderColor.R}, G={borderColor.G}, B={borderColor.B}");
            System.Diagnostics.Debug.WriteLine($"🎨 Interface accent: R={interfaceColor.R}, G={interfaceColor.G}, B={interfaceColor.B}");

            // Ресурсы
            Application.Current.Resources["SystemAccentBrush"] = interfaceBrush;
            Application.Current.Resources["SystemAccentLightBrush"] = interfaceLightBrush;
            Application.Current.Resources["Theme.PrimaryAccentColor"] = interfaceColor;
            Application.Current.Resources["MahApps.Brushes.Accent"] = interfaceBrush;
            Application.Current.Resources["MahApps.Brushes.Accent2"] = interfaceBrush;
            Application.Current.Resources["MahApps.Brushes.Accent3"] = interfaceBrush;
            Application.Current.Resources["MahApps.Brushes.Accent4"] = interfaceBrush;
            Application.Current.Resources["MahApps.Brushes.AccentBase"] = interfaceBrush;
            Application.Current.Resources["MahApps.Colors.Accent"] = interfaceColor;
            Application.Current.Resources["MahApps.Colors.AccentBase"] = interfaceColor;
            Application.Current.Resources["WindowBorderBrush"] = borderBrush;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ Error loading accent: {ex.Message}");
        }
    }

    private async void OnExit(object sender, ExitEventArgs e)
    {
        await _host.StopAsync();
        _host.Dispose();
        _host = null;
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // TODO: Please log and handle the exception as appropriate to your scenario
        System.Diagnostics.Debug.WriteLine($"❌ Unhandled exception: {e.Exception.Message}");
    }
}