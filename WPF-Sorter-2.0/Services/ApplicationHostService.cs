using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Diagnostics;
using WPF_Sorter_2._0.Contracts.Activation;
using WPF_Sorter_2._0.Contracts.Services;
using WPF_Sorter_2._0.Contracts.Views;
using WPF_Sorter_2._0.ViewModels;

namespace WPF_Sorter_2._0.Services;

public class ApplicationHostService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly INavigationService _navigationService;
    private readonly IToastNotificationsService _toastNotificationsService;
    private readonly IPersistAndRestoreService _persistAndRestoreService;
    private readonly IThemeSelectorService _themeSelectorService;
    private readonly IEnumerable<IActivationHandler> _activationHandlers;
    private IShellWindow _shellWindow;
    private bool _isInitialized;

    public ApplicationHostService(
        IServiceProvider serviceProvider,
        IEnumerable<IActivationHandler> activationHandlers,
        INavigationService navigationService,
        IThemeSelectorService themeSelectorService,
        IPersistAndRestoreService persistAndRestoreService,
        IToastNotificationsService toastNotificationsService)
    {
        _serviceProvider = serviceProvider;
        _activationHandlers = activationHandlers;
        _navigationService = navigationService;
        _themeSelectorService = themeSelectorService;
        _persistAndRestoreService = persistAndRestoreService;
        _toastNotificationsService = toastNotificationsService;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await InitializeAsync();
        await HandleActivationAsync();
        await StartupAsync();

        // 👇 Фоновая проверка обновлений через 5 секунд
        _ = Task.Run(async () =>
        {
            await Task.Delay(5000);
            try
            {
                var updateService = _serviceProvider.GetService<UpdateService>();
                if (updateService != null)
                {
                    await updateService.CheckForUpdatesAsync(true);
                }
                else
                {
                    Debug.WriteLine("⚠️ UpdateService not available for background check");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Background update check failed: {ex.Message}");
            }
        });

        _isInitialized = true;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _persistAndRestoreService.PersistData();
        await Task.CompletedTask;
    }

    private async Task InitializeAsync()
    {
        if (!_isInitialized)
        {
            _persistAndRestoreService.RestoreData();
            _themeSelectorService.InitializeTheme();
            await Task.CompletedTask;
        }
    }

    private async Task StartupAsync()
    {
        if (!_isInitialized)
        {
            _toastNotificationsService.ShowToastNotificationSample();
            await Task.CompletedTask;
        }
    }

    private async Task HandleActivationAsync()
    {
        var activationHandler = _activationHandlers.FirstOrDefault(h => h.CanHandle());

        if (activationHandler != null)
        {
            await activationHandler.HandleAsync();
        }

        await Task.CompletedTask;

        if (App.Current.Windows.OfType<IShellWindow>().Count() == 0)
        {
            _shellWindow = _serviceProvider.GetService(typeof(IShellWindow)) as IShellWindow;
            _navigationService.Initialize(_shellWindow.GetNavigationFrame());
            _shellWindow.ShowWindow();
            _navigationService.NavigateTo(typeof(MainViewModel).FullName);
            await Task.CompletedTask;
        }
    }
}