using Microsoft.Win32;
using System.Windows.Media;

namespace WPF_Sorter_2._0.Services;

public static class SystemAccentService
{
    /// <summary>
    /// Получает системный акцентный цвет из реестра (чистый, без изменений)
    /// </summary>
    public static Color GetSystemAccentColor()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM");
            if (key != null)
            {
                var accentValue = key.GetValue("AccentColor");
                if (accentValue is int accentInt)
                {
                    return ParseDwordColor(accentInt);
                }
                if (accentValue is string accentStr && !string.IsNullOrEmpty(accentStr))
                {
                    return ParseHexColor(accentStr);
                }

                var colorizationValue = key.GetValue("ColorizationColor");
                if (colorizationValue is int colorizationInt)
                {
                    return ParseDwordColor(colorizationInt);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ Error reading accent: {ex.Message}");
        }

        return Color.FromRgb(0x00, 0x78, 0xD7);
    }

    /// <summary>
    /// Проверяет, включён ли параметр "Показывать контрастный цвет для заголовков и границ окон"
    /// </summary>
    public static bool IsColorPrevalenceEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM");
            if (key != null)
            {
                var value = key.GetValue("ColorPrevalence") as int?;
                return value.HasValue && value.Value == 1;
            }
        }
        catch { }
        return false;
    }

    /// <summary>
    /// Возвращает цвет акцента с учётом ColorPrevalence (для интерфейса)
    /// </summary>
    public static Color GetInterfaceAccentColor()
    {
        var color = GetSystemAccentColor();
        var isColorPrevalence = IsColorPrevalenceEnabled();

        System.Diagnostics.Debug.WriteLine($"🎨 System accent: R={color.R}, G={color.G}, B={color.B}");
        System.Diagnostics.Debug.WriteLine($"🎨 ColorPrevalence: {(isColorPrevalence ? "ON (яркий)" : "OFF (приглушённый)")}");

        // Если ColorPrevalence выключен — приглушаем цвет (только для интерфейса)
        if (!isColorPrevalence)
        {
            var gray = (byte)(color.R * 0.3 + color.G * 0.59 + color.B * 0.11);
            color = Color.FromRgb(
                (byte)(color.R * 0.6 + gray * 0.4),
                (byte)(color.G * 0.6 + gray * 0.4),
                (byte)(color.B * 0.6 + gray * 0.4)
            );
            System.Diagnostics.Debug.WriteLine($"🎨 Interface accent (desaturated): R={color.R}, G={color.G}, B={color.B}");
        }

        return color;
    }

    private static Color ParseDwordColor(int value)
    {
        var a = (byte)((value >> 24) & 0xFF);
        var r = (byte)(value & 0xFF);
        var g = (byte)((value >> 8) & 0xFF);
        var b = (byte)((value >> 16) & 0xFF);
        return Color.FromArgb(a, r, g, b);
    }

    private static Color ParseHexColor(string hex)
    {
        hex = hex.Replace("0x", "").Replace("#", "").Trim();

        if (hex.Length == 8 && int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out int value))
        {
            return ParseDwordColor(value);
        }

        if (hex.Length == 6 && int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out int rgb))
        {
            var r = (byte)((rgb >> 16) & 0xFF);
            var g = (byte)((rgb >> 8) & 0xFF);
            var b = (byte)(rgb & 0xFF);
            return Color.FromRgb(r, g, b);
        }

        return Color.FromRgb(0x00, 0x78, 0xD7);
    }
}