using CommunityToolkit.WinUI.Notifications;

using Windows.UI.Notifications;

using WPF_Sorter_2._0.Contracts.Services;

namespace WPF_Sorter_2._0.Services;

public partial class ToastNotificationsService : IToastNotificationsService
{
    public ToastNotificationsService()
    {
    }

    public void ShowToastNotification(ToastNotification toastNotification)
    {
        ToastNotificationManagerCompat.CreateToastNotifier().Show(toastNotification);
    }
}
