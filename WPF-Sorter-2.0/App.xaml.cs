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
    private readonly object _serviceLock = new();

    public T GetService<T>() where T : class
    {
        if (_host == null) return null;
        return _host.Services.GetService(typeof(T)) as T;
    }

    public App()
    {
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ FATAL: {ex.Message}");
            // TODO: Логирование в файл
        }
    }

    private async void OnStartup(object sender, StartupEventArgs e)
    {
        try
        {
            ToastNotificationManagerCompat.OnActivated += OnToastActivated;

            var activationArgs = new Dictionary<string, string>
            {
                { ToastNotificationActivationHandler.ActivationArguments, string.Empty }
            };
            var appLocation = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) ?? string.Empty;

            _host = Host.CreateDefaultBuilder(e.Args)
                    .ConfigureAppConfiguration(c =>
                    {
                        c.SetBasePath(appLocation);
                        c.AddInMemoryCollection(activationArgs);
                        c.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
                    })
                    .ConfigureServices(ConfigureServices)
                    .Build();

            LoadSystemAccent();

            if (ToastNotificationManagerCompat.WasCurrentProcessToastActivated())
            {
                return;
            }

            await _host.StartAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ Startup error: {ex.Message}");
            MessageBox.Show($"Ошибка запуска: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    private void OnToastActivated(ToastNotificationActivatedEventArgsCompat toastArgs)
    {
        try
        {
            Current.Dispatcher.Invoke(async () =>
            {
                var config = GetService<IConfiguration>();
                if (config != null)
                {
                    ((IConfigurationRoot)config)[ToastNotificationActivationHandler.ActivationArguments] = toastArgs.Argument;
                }
                await _host?.StartAsync();
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ Toast activation error: {ex.Message}");
        }
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

        // Background Service (Singleton)
        services.AddSingleton<SortBackgroundService>();

        // UI Services
        services.AddSingleton<IToastNotificationsService, ToastNotificationsService>();
        services.AddSingleton<IApplicationInfoService, ApplicationInfoService>();
        services.AddSingleton<ISystemService, SystemService>();
        services.AddSingleton<IPersistAndRestoreService, PersistAndRestoreService>();
        services.AddSingleton<IThemeSelectorService, ThemeSelectorService>();
        services.AddSingleton<IPageService, PageService>();
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<SettingsService>();

        // Update Service
        services.AddSingleton<UpdateService>();

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
            if (Application.Current == null) return;
            var resources = Application.Current.Resources;

            var borderColor = SystemAccentService.GetSystemAccentColor();
            var borderBrush = new SolidColorBrush(borderColor);

            var interfaceColor = SystemAccentService.GetInterfaceAccentColor();
            var interfaceBrush = new SolidColorBrush(interfaceColor);
            var interfaceLightBrush = new SolidColorBrush(Color.FromArgb(0x40, interfaceColor.R, interfaceColor.G, interfaceColor.B));

            // Lock resource dictionary for thread safety
            lock (resources)
            {
                resources["SystemAccentBrush"] = interfaceBrush;
                resources["SystemAccentLightBrush"] = interfaceLightBrush;
                resources["Theme.PrimaryAccentColor"] = interfaceColor;

                var accentKeys = new[]
                {
                    "MahApps.Brushes.Accent",
                    "MahApps.Brushes.Accent2",
                    "MahApps.Brushes.Accent3",
                    "MahApps.Brushes.Accent4",
                    "MahApps.Brushes.AccentBase",
                    "MahApps.Colors.Accent",
                    "MahApps.Colors.AccentBase"
                };

                foreach (var key in accentKeys)
                {
                    if (key.Contains("Color"))
                    {
                        resources[key] = interfaceColor;
                    }
                    else
                    {
                        resources[key] = interfaceBrush;
                    }
                }

                resources["WindowBorderBrush"] = borderBrush;
            }

            System.Diagnostics.Debug.WriteLine($"🎨 Accent loaded successfully");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ Error loading accent: {ex.Message}");
        }
    }

    private async void OnExit(object sender, ExitEventArgs e)
    {
        try
        {
            if (_host != null)
            {
                await _host.StopAsync();
                _host.Dispose();
                _host = null;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ Exit error: {ex.Message}");
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"❌ Unhandled exception: {e.Exception.Message}");
        System.Diagnostics.Debug.WriteLine($"Stack: {e.Exception.StackTrace}");

        // TODO: Логирование в файл

        // Показываем пользователю
        MessageBox.Show(
            $"Произошла ошибка: {e.Exception.Message}\n\nПодробности записаны в лог.",
            "Ошибка",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        e.Handled = true;
    }
}