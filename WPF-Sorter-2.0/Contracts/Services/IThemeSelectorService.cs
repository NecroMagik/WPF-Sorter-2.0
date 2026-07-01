using WPF_Sorter_2._0.Models;

namespace WPF_Sorter_2._0.Contracts.Services;

public interface IThemeSelectorService
{
    void InitializeTheme();

    void SetTheme(AppTheme theme);

    AppTheme GetCurrentTheme();
}
