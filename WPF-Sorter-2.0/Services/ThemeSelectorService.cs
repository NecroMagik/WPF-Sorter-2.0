using ControlzEx.Theming;
using MahApps.Metro.Theming;
using System.Windows;
using System.Windows.Media;
using WPF_Sorter_2._0.Contracts.Services;
using WPF_Sorter_2._0.Models;

namespace WPF_Sorter_2._0.Services;

public class ThemeSelectorService : IThemeSelectorService
{
    private const string HcDarkTheme = "pack://application:,,,/Styles/Themes/HC.Dark.Blue.xaml";
    private const string HcLightTheme = "pack://application:,,,/Styles/Themes/HC.Light.Blue.xaml";

    public void InitializeTheme()
    {
        try
        {
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
                ThemeManager.Current.ChangeTheme(Application.Current, $"{theme}.Blue", SystemParameters.HighContrast);
            }

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

            // 👇 Цвет для границ окон — всегда яркий (системный)
            var borderColor = SystemAccentService.GetSystemAccentColor();
            var borderBrush = new SolidColorBrush(borderColor);

            // 👇 Цвет для интерфейса — с учётом ColorPrevalence
            var interfaceColor = SystemAccentService.GetInterfaceAccentColor();
            var interfaceBrush = new SolidColorBrush(interfaceColor);
            var interfaceLightBrush = new SolidColorBrush(Color.FromArgb(0x40, interfaceColor.R, interfaceColor.G, interfaceColor.B));

            System.Diagnostics.Debug.WriteLine($"🎨 Border accent: R={borderColor.R}, G={borderColor.G}, B={borderColor.B}");
            System.Diagnostics.Debug.WriteLine($"🎨 Interface accent: R={interfaceColor.R}, G={interfaceColor.G}, B={interfaceColor.B}");

            // 👇 Обновляем ресурсы
            UpdateResources(borderColor, borderBrush, interfaceColor, interfaceBrush, interfaceLightBrush);

            // 👇 Обновляем окна
            UpdateWindows(borderBrush);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error applying system accent: {ex.Message}");
        }
    }

    private void UpdateResources(Color borderColor, SolidColorBrush borderBrush, Color interfaceColor, SolidColorBrush interfaceBrush, SolidColorBrush interfaceLightBrush)
    {
        var resources = Application.Current.Resources;

        // Системные ресурсы (интерфейс)
        SetResource(resources, "SystemAccentBrush", interfaceBrush);
        SetResource(resources, "SystemAccentLightBrush", interfaceLightBrush);

        // Обязательные для MahApps (интерфейс)
        SetColorResource(resources, "Theme.PrimaryAccentColor", interfaceColor);
        SetResource(resources, "Theme.PrimaryAccentColorBrush", interfaceBrush);

        // 👇 MahApps акценты (интерфейс)
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
            if (key.Contains("Color"))
            {
                SetColorResource(resources, key, interfaceColor);
            }
            else
            {
                SetResource(resources, key, interfaceBrush);
            }
        }

        // 👇 Границы окон (всегда яркие)
        SetResource(resources, "WindowBorderBrush", borderBrush);

        // Обновляем словари
        foreach (var dict in resources.MergedDictionaries)
        {
            if (dict.Contains("Theme.PrimaryAccentColor"))
            {
                dict["Theme.PrimaryAccentColor"] = interfaceColor;
            }

            foreach (var key in accentKeys)
            {
                if (dict.Contains(key))
                {
                    dict[key] = key.Contains("Color") ? (object)interfaceColor : interfaceBrush;
                }
            }
        }
    }

    private void UpdateWindows(SolidColorBrush borderBrush)
    {
        try
        {
            foreach (Window window in Application.Current.Windows)
            {
                if (window is MahApps.Metro.Controls.MetroWindow metroWindow)
                {
                    // 👇 Границы окон — всегда яркие
                    metroWindow.BorderBrush = borderBrush;
                    metroWindow.GlowBrush = borderBrush;
                    metroWindow.NonActiveBorderBrush = borderBrush;
                    metroWindow.NonActiveGlowBrush = borderBrush;

                    metroWindow.UpdateLayout();
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error updating windows: {ex.Message}");
        }
    }

    private void SetResource(ResourceDictionary resources, string key, object value)
    {
        if (resources.Contains(key))
            resources[key] = value;
        else
            resources.Add(key, value);
    }

    private void SetColorResource(ResourceDictionary resources, string key, Color color)
    {
        if (resources.Contains(key))
            resources[key] = color;
        else
            resources.Add(key, color);
    }
}