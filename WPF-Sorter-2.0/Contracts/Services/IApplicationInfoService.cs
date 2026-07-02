namespace WPF_Sorter_2._0.Contracts.Services;

public interface IApplicationInfoService
{
    string GetVersion();
    string GetProductName();
    string GetAssemblyName();
}