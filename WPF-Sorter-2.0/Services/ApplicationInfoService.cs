using System.Diagnostics;
using System.Reflection;

using WPF_Sorter_2._0.Contracts.Services;

namespace WPF_Sorter_2._0.Services;

public class ApplicationInfoService : IApplicationInfoService
{
    public ApplicationInfoService()
    {
    }

    public Version GetVersion()
    {
        // Set the app version in WPF-Sorter-2.0 > Properties > Package > PackageVersion
        string assemblyLocation = Assembly.GetExecutingAssembly().Location;
        var version = FileVersionInfo.GetVersionInfo(assemblyLocation).FileVersion;
        return new Version(version);
    }
}
