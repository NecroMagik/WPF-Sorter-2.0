using Microsoft.Extensions.Configuration;
using System.Windows;
using WPF_Sorter_2._0.Contracts.Activation;
using WPF_Sorter_2._0.Contracts.Services;
using WPF_Sorter_2._0.Contracts.Views;
using WPF_Sorter_2._0.Services;
using WPF_Sorter_2._0.ViewModels;
using WPF_Sorter_2._0.Views;

namespace WPF_Sorter_2._0.Activation;

public class ToastNotificationActivationHandler : IActivationHandler
{
    public const string ActivationArguments = "ToastNotificationActivationArguments";

    private readonly IConfiguration _config;
    private readonly IServiceProvider _serviceProvider;
    private readonly INavigationService _navigationService;
    private readonly UpdateService _updateService;

    public ToastNotificationActivationHandler(
        IConfiguration config,
        IServiceProvider serviceProvider,
        INavigationService navigationService,
        UpdateService updateService)
    {
        _config = config;
        _serviceProvider = serviceProvider;
        _navigationService = navigationService;
        _updateService = updateService;
    }

    public bool CanHandle()
    {
        var args = _config[ActivationArguments];
        return !string.IsNullOrEmpty(args) && args.StartsWith("update_");
    }

    public async Task HandleAsync()
    {
        var args = _config[ActivationArguments] ?? string.Empty;
        System.Diagnostics.Debug.WriteLine($"📢 Toast activated with args: {args}");

        if (args.StartsWith("update_install"))
        {
            await Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                // Проверяем обновление снова
                var updateInfo = await _updateService.CheckForUpdatesAsync(false);
                if (updateInfo != null)
                {
                    var dialog = new UpdateDialog(updateInfo, _updateService.GetCurrentVersion(), _updateService);
                    dialog.Owner = Application.Current.MainWindow;
                    var result = dialog.ShowDialog();

                    if (result == true && dialog.IsInstallConfirmed)
                    {
                        await _updateService.DownloadAndInstallUpdateAsync(updateInfo);
                    }
                }
            });
        }
        else if (args.StartsWith("update_later"))
        {
            // Ничего не делаем, просто закрываем
        }
        else if (args.StartsWith("update_available"))
        {
            // Открываем диалог обновления
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                _navigationService.NavigateTo(typeof(SettingsViewModel).FullName);
            });
        }

        // Активируем главное окно
        if (App.Current.Windows.OfType<IShellWindow>().Count() == 0)
        {
            var shellWindow = _serviceProvider.GetService(typeof(IShellWindow)) as IShellWindow;
            shellWindow?.ShowWindow();
        }
        else if (App.Current.MainWindow != null)
        {
            App.Current.MainWindow.Activate();
            if (App.Current.MainWindow.WindowState == WindowState.Minimized)
            {
                App.Current.MainWindow.WindowState = WindowState.Normal;
            }
        }

        await Task.CompletedTask;
    }
}