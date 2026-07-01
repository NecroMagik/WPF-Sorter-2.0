namespace WPF_Sorter_2._0.Core.Models;

public class SortProfile
{
    public string Name { get; set; } = "Основной профиль";
    public string SourceFolder { get; set; } = string.Empty;
    public List<SortRule> Rules { get; set; } = new();
    public bool IncludeSubfolders { get; set; } = true;
    public bool MoveInsteadOfCopy { get; set; } = true;
    public bool OverwriteExisting { get; set; } = false;
    public string DefaultCategoryPath { get; set; } = "Прочее";
}