using System.Windows;
using System.Windows.Media;
using ControlzEx.Theming;
using MahApps.Metro.Theming;
using WPF_Sorter_2._0.Contracts.Services;
using WPF_Sorter_2._0.Models;

namespace WPF_Sorter_2._0.Services;

public class ThemeSelectorService : IThemeSelectorService
{
    private const string HcDarkTheme = "pack://application:,,,/Styles/Themes/HC.Dark.Blue.xaml";
    private const string HcLightTheme = "pack://application:,,,/Styles/Themes/HC.Light.Blue.xaml";

    public ThemeSelectorService()
    {
    }

    public void InitializeTheme()
    {
        try
        {
            // Регистрируем кастомные темы для высокого контраста
            ThemeManager.Current.AddLibraryTheme(new LibraryTheme(new Uri(HcDarkTheme), MahAppsLibraryThemeProvider.DefaultInstance));
            ThemeManager.Current.AddLibraryTheme(new LibraryTheme(new Uri(HcLightTheme), MahAppsLibraryThemeProvider.DefaultInstance));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error adding themes: {ex.Message}");
        }

        var theme = GetCurrentTheme();
        SetTheme(theme);
    }

    public void SetTheme(AppTheme theme)
    {
        try
        {
            if (theme == AppTheme.Default)
            {
                ThemeManager.Current.ThemeSyncMode = ThemeSyncMode.SyncAll;
                ThemeManager.Current.SyncTheme();
            }
            else
            {
                ThemeManager.Current.ThemeSyncMode = ThemeSyncMode.SyncWithHighContrast;
                ThemeManager.Current.SyncTheme();

                // Используем ChangeTheme для смены базовой темы (светлая/тёмная)
                // Акцент будет переопределён после
                ThemeManager.Current.ChangeTheme(Application.Current, $"{theme}.Blue", SystemParameters.HighContrast);
            }

            // 👇 ВСЕГДА применяем системный акцент ПОСЛЕ смены темы
            ApplySystemAccent();

            if (Application.Current != null)
            {
                Application.Current.Properties["Theme"] = theme.ToString();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error setting theme: {ex.Message}");
        }
    }

    public AppTheme GetCurrentTheme()
    {
        if (Application.Current != null && Application.Current.Properties.Contains("Theme"))
        {
            var themeName = Application.Current.Properties["Theme"].ToString();
            if (Enum.TryParse(themeName, out AppTheme theme))
            {
                return theme;
            }
        }

        return AppTheme.Default;
    }

    public void ApplySystemAccent()
    {
        try
        {
            if (Application.Current == null) return;

            var accentColor = SystemAccentService.GetSystemAccentColor();
            var accentBrush = new SolidColorBrush(accentColor);
            var accentLightBrush = new SolidColorBrush(Color.FromArgb(0x40, accentColor.R, accentColor.G, accentColor.B));

            System.Diagnostics.Debug.WriteLine($"🎨 Applying system accent: R={accentColor.R}, G={accentColor.G}, B={accentColor.B}");

            // 👇 1. Обновляем SystemAccentBrush
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

            // 👇 2. Переопределяем все акценты MahApps
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

            // 👇 3. Обновляем все словари
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
            System.Diagnostics.Debug.WriteLine($"Error applying system accent: {ex.Message}");
        }
    }
}