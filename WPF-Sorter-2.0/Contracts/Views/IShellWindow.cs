using System.Windows.Controls;

namespace WPF_Sorter_2._0.Contracts.Views;

public interface IShellWindow
{
    Frame GetNavigationFrame();

    void ShowWindow();

    void CloseWindow();
}
