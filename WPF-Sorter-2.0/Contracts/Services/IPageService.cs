using System.Windows.Controls;

namespace WPF_Sorter_2._0.Contracts.Services;

public interface IPageService
{
    Type GetPageType(string key);

    Page GetPage(string key);
}
