using System.Windows.Controls;

using WPF_Sorter_2._0.ViewModels;

namespace WPF_Sorter_2._0.Views;

public partial class MainPage : Page
{
    public MainPage(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
