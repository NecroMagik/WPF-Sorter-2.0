using Microsoft.Win32;
using System.Windows.Media;

namespace WPF_Sorter_2._0.Services;

public class SystemThemeService : IDisposable
{
    public event Action<Color> AccentColorChanged;
    public event Action<bool> IsDarkModeChanged;

    public SystemThemeService()
    {
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category == UserPreferenceCategory.Color ||
            e.Category == UserPreferenceCategory.General)
        {
            var accent = SystemAccentService.GetSystemAccentColor();
            AccentColorChanged?.Invoke(accent);

            // Проверяем, тёмная ли тема
            var isDark = IsSystemDarkMode();
            IsDarkModeChanged?.Invoke(isDark);
        }
    }

    private bool IsSystemDarkMode()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            if (key != null)
            {
                var value = key.GetValue("AppsUseLightTheme");
                if (value is int intValue)
                {
                    return intValue == 0;
                }
            }
        }
        catch { }
        return false;
    }

    public void Dispose()
    {
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
    }
}