using MahApps.Metro.Controls;
using Microsoft.Win32;
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using WPF_Sorter_2._0.Contracts.Services;
using WPF_Sorter_2._0.Contracts.Views;
using WPF_Sorter_2._0.Services;
using WPF_Sorter_2._0.ViewModels;

namespace WPF_Sorter_2._0.Views;

public partial class ShellWindow : MetroWindow, IShellWindow
{
    // Импорт функции для изменения стиля окна
    [DllImport("user32.dll")]
    private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, DwmWindowAttribute dwAttribute, ref int pvAttribute, int cbAttribute);

    private enum DwmWindowAttribute
    {
        UseImmersiveDarkMode = 20,
        CornerPreference = 33
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowCompositionAttributeData
    {
        public WindowCompositionAttribute Attribute;
        public IntPtr Data;
        public int SizeOfData;
    }

    private enum WindowCompositionAttribute
    {
        WCA_ACCENT_POLICY = 19
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

        // Включаем закруглённые углы (Windows 11)
        TryEnableRoundCorners(hwnd);

        // Подписываемся на смену темы системы
        SystemEvents.UserPreferenceChanged += OnSystemThemeChanged;
    }

    private void TryEnableRoundCorners(IntPtr hwnd)
    {
        try
        {
            // Для Windows 11 22000+ 
            var cornerPreference = 2; // 0=Default, 1=DoNotRound, 2=Round, 3=RoundSmall
            DwmSetWindowAttribute(hwnd, DwmWindowAttribute.CornerPreference, ref cornerPreference, sizeof(int));
        }
        catch
        {
            // Игнорируем на старых версиях Windows
        }
    }

    private void OnSystemThemeChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        // Обновляем тему при смене системной
        try
        {
            // Получаем сервис через App (используем приведение к App)
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

    public System.Windows.Controls.Frame GetNavigationFrame()
    {
        return shellFrame;
    }

    public void ShowWindow()
    {
        Show();
    }

    public void CloseWindow()
    {
        Close();
    }
}