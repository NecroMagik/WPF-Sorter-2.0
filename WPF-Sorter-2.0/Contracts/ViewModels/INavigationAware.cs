namespace WPF_Sorter_2._0.Contracts.ViewModels;

public interface INavigationAware
{
    void OnNavigatedTo(object parameter);
    void OnNavigatedFrom();
}