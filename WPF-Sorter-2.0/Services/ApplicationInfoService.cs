using System.Reflection;
using WPF_Sorter_2._0.Contracts.Services;

namespace WPF_Sorter_2._0.Services;

public class ApplicationInfoService : IApplicationInfoService
{
    public string GetVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var fileVersion = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;
        var version = assembly.GetName().Version?.ToString();

        // 👇 Берём из AssemblyFileVersion (она соответствует FileVersion в csproj)
        if (!string.IsNullOrEmpty(fileVersion))
        {
            return fileVersion;
        }

        return version ?? "1.0.0.0";
    }

    public string GetProductName()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var title = assembly.GetCustomAttribute<AssemblyTitleAttribute>()?.Title;
        return title ?? "WPF-Sorter-2.0";
    }

    public string GetAssemblyName()
    {
        return Assembly.GetExecutingAssembly().GetName().Name ?? "WPF-Sorter-2.0";
    }
}