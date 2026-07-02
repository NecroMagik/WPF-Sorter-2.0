using Microsoft.Win32;
using System.Windows.Media;

namespace WPF_Sorter_2._0.Services;

public static class SystemAccentService
{
    public static Color GetSystemAccentColor()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM");
            if (key != null)
            {
                System.Diagnostics.Debug.WriteLine("🔍 Registry key found: Software\\Microsoft\\Windows\\DWM");

                // 👇 1. Пробуем AccentColor как DWORD (число)
                var accentColorValue = key.GetValue("AccentColor");
                System.Diagnostics.Debug.WriteLine($"📌 AccentColor raw: {accentColorValue} (type: {accentColorValue?.GetType().Name ?? "NULL"})");

                if (accentColorValue != null)
                {
                    // Пробуем получить как int (DWORD)
                    if (accentColorValue is int accentInt)
                    {
                        System.Diagnostics.Debug.WriteLine($"📌 AccentColor as int: 0x{accentInt:X8}");
                        var color = ParseDwordColor(accentInt);
                        System.Diagnostics.Debug.WriteLine($"✅ Parsed AccentColor (DWORD): R={color.R}, G={color.G}, B={color.B}");
                        return color;
                    }

                    // Пробуем получить как string
                    if (accentColorValue is string accentString && !string.IsNullOrEmpty(accentString))
                    {
                        System.Diagnostics.Debug.WriteLine($"📌 AccentColor as string: {accentString}");
                        var color = ParseAccentColor(accentString);
                        System.Diagnostics.Debug.WriteLine($"✅ Parsed AccentColor (string): R={color.R}, G={color.G}, B={color.B}");
                        return color;
                    }
                }

                System.Diagnostics.Debug.WriteLine("⚠️ AccentColor not found or invalid");

                // 👇 2. Пробуем ColorizationColor (запасной вариант)
                var colorizationValue = key.GetValue("ColorizationColor");
                System.Diagnostics.Debug.WriteLine($"📌 ColorizationColor raw: {colorizationValue} (type: {colorizationValue?.GetType().Name ?? "NULL"})");

                if (colorizationValue is int colorizationInt)
                {
                    System.Diagnostics.Debug.WriteLine($"📌 ColorizationColor as int: 0x{colorizationInt:X8}");
                    var color = ParseDwordColor(colorizationInt);
                    System.Diagnostics.Debug.WriteLine($"✅ Parsed ColorizationColor: R={color.R}, G={color.G}, B={color.B}");
                    return color;
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("❌ Registry key NOT found: Software\\Microsoft\\Windows\\DWM");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"❌ Error reading accent: {ex.Message}");
        }

        System.Diagnostics.Debug.WriteLine("⚠️ Fallback to default blue (0x0078D7)");
        return Color.FromRgb(0x00, 0x78, 0xD7);
    }

    /// <summary>
    /// Парсит DWORD цвет из реестра (формат: AABBGGRR)
    /// </summary>
    private static Color ParseDwordColor(int value)
    {
        // Формат: AABBGGRR (Windows DWM формат)
        // Пример: 0xFF5E00EA
        //   AA = FF (Alpha)
        //   BB = 5E (Blue)
        //   GG = 00 (Green)  
        //   RR = EA (Red)
        var a = (byte)((value >> 24) & 0xFF);
        var r = (byte)(value & 0xFF);           // Младший байт = Red
        var g = (byte)((value >> 8) & 0xFF);    // Второй байт = Green
        var b = (byte)((value >> 16) & 0xFF);   // Третий байт = Blue

        System.Diagnostics.Debug.WriteLine($"📊 DWORD: 0x{value:X8} -> R={r}, G={g}, B={b}, A={a}");
        return Color.FromArgb(a, r, g, b);
    }

    private static Color ParseAccentColor(string hexColor)
    {
        var cleanHex = hexColor.Replace("0x", "").Replace("#", "").Trim();

        System.Diagnostics.Debug.WriteLine($"🔍 Parsing AccentColor string: '{hexColor}' -> clean: '{cleanHex}'");

        if (cleanHex.Length == 8 && int.TryParse(cleanHex, System.Globalization.NumberStyles.HexNumber, null, out int value))
        {
            return ParseDwordColor(value);
        }

        if (cleanHex.Length == 6 && int.TryParse(cleanHex, System.Globalization.NumberStyles.HexNumber, null, out int rgb))
        {
            var r = (byte)((rgb >> 16) & 0xFF);
            var g = (byte)((rgb >> 8) & 0xFF);
            var b = (byte)(rgb & 0xFF);

            System.Diagnostics.Debug.WriteLine($"📊 RGB: 0x{rgb:X6} -> R={r}, G={g}, B={b}");
            return Color.FromRgb(r, g, b);
        }

        try
        {
            var color = (Color)System.Windows.Media.ColorConverter.ConvertFromString(hexColor);
            System.Diagnostics.Debug.WriteLine($"📊 Converted via ColorConverter: R={color.R}, G={color.G}, B={color.B}");
            return color;
        }
        catch { }

        System.Diagnostics.Debug.WriteLine($"⚠️ Failed to parse: {hexColor}");
        return Color.FromRgb(0x00, 0x78, 0xD7);
    }
}