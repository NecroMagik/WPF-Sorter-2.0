using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

using CommunityToolkit.WinUI.Notifications;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
            var accentColor = SystemAccentService.GetSystemAccentColor();
            var accentBrush = new SolidColorBrush(accentColor);
            var accentLightBrush = new SolidColorBrush(Color.FromArgb(0x40, accentColor.R, accentColor.G, accentColor.B));

            System.Diagnostics.Debug.WriteLine($"🎨 Loading system accent: R={accentColor.R}, G={accentColor.G}, B={accentColor.B}");

            // 👇 Обновляем SystemAccentBrush
            if (Application.Current.Resources.Contains("SystemAccentBrush"))
            {
                Application.Current.Resources["SystemAccentBrush"] = accentBrush;
            }
            else
            {
                Application.Current.Resources.Add("SystemAccentBrush", accentBrush);
            }

            if (Application.Current.Resources.Contains("SystemAccentLightBrush"))
            {
                Application.Current.Resources["SystemAccentLightBrush"] = accentLightBrush;
            }
            else
            {
                Application.Current.Resources.Add("SystemAccentLightBrush", accentLightBrush);
            }

            // 👇 Переопределяем все акценты MahApps
            var accentKeys = new[]
            {
            "MahApps.Brushes.Accent",
            "MahApps.Brushes.Accent2",
            "MahApps.Brushes.Accent3",
            "MahApps.Brushes.Accent4",
            "MahApps.Brushes.AccentBase",
            "MahApps.Brushes.AccentColorBrush",
            "MahApps.Colors.Accent",
            "MahApps.Colors.AccentBase"
        };

            foreach (var key in accentKeys)
            {
                if (Application.Current.Resources.Contains(key))
                {
                    Application.Current.Resources[key] = accentBrush;
                }
                else
                {
                    Application.Current.Resources.Add(key, accentBrush);
                }
            }

            // 👇 Обновляем все словари
            foreach (var dict in Application.Current.Resources.MergedDictionaries)
            {
                foreach (var key in accentKeys)
                {
                    if (dict.Contains(key))
                    {
                        dict[key] = accentBrush;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ Error loading system accent: {ex.Message}");

            // Fallback на синий
            var fallbackColor = Color.FromRgb(0x00, 0x78, 0xD7);
            var fallbackBrush = new SolidColorBrush(fallbackColor);

            if (Application.Current.Resources.Contains("SystemAccentBrush"))
            {
                Application.Current.Resources["SystemAccentBrush"] = fallbackBrush;
            }
            else
            {
                Application.Current.Resources.Add("SystemAccentBrush", fallbackBrush);
            }

            Application.Current.Resources["MahApps.Brushes.Accent"] = fallbackBrush;
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