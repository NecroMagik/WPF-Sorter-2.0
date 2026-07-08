using MahApps.Metro.Controls;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using WPF_Sorter_2._0.Contracts.Services;
using WPF_Sorter_2._0.Contracts.Views;
using WPF_Sorter_2._0.ViewModels;

namespace WPF_Sorter_2._0.Views;

public partial class ShellWindow : MetroWindow, IShellWindow
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, DwmWindowAttribute dwAttribute, ref int pvAttribute, int cbAttribute);

    private enum DwmWindowAttribute
    {
        CornerPreference = 33
    }

    public ShellWindow(ShellViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        SourceInitialized += OnSourceInitialized;
    }

    private void OnSourceInitialized(object sender, EventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        TryEnableRoundCorners(hwnd);
        SystemEvents.UserPreferenceChanged += OnSystemThemeChanged;
    }

    private void TryEnableRoundCorners(IntPtr hwnd)
    {
        try
        {
            var cornerPreference = 2;
            DwmSetWindowAttribute(hwnd, DwmWindowAttribute.CornerPreference, ref cornerPreference, sizeof(int));
        }
        catch { }
    }

    private void OnSystemThemeChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        try
        {
            var app = Application.Current as App;
            if (app != null)
            {
                var themeService = app.GetService<IThemeSelectorService>();
                if (themeService != null)
                {
                    themeService.InitializeTheme();
                }
            }
        }
        catch { }
    }

    protected override void OnClosed(EventArgs e)
    {
        SystemEvents.UserPreferenceChanged -= OnSystemThemeChanged;
        base.OnClosed(e);
    }

    public System.Windows.Controls.Frame GetNavigationFrame() => shellFrame;
    public void ShowWindow() => Show();
    public void CloseWindow() => Close();
}