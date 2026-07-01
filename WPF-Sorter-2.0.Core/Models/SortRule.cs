namespace WPF_Sorter_2._0.Core.Models;

public class SortRule
{
    public string Name { get; set; } = string.Empty;
    public string IconGlyph { get; set; } = string.Empty;
    public List<string> Extensions { get; set; } = new();
    public string DestinationFolder { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
}