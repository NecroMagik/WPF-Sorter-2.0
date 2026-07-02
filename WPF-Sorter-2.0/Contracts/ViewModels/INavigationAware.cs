namespace WPF_Sorter_2._0.Contracts.ViewModels
{
    internal interface INavigationAware
    {
        void OnNavigatedTo(object parameter);
        void OnNavigatedFrom();
    }
}