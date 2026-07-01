namespace WPF_Sorter_2._0.Core.Models;

public class SortProgress
{
    public int TotalFiles { get; set; }
    public int ProcessedFiles { get; set; }
    public string CurrentFile { get; set; } = string.Empty;
    public string CurrentStatus { get; set; } = string.Empty;
    public int HashCheckPassed { get; set; }
    public int HashCheckFailed { get; set; }
    public double ProgressPercentage => TotalFiles > 0
        ? (double)ProcessedFiles / TotalFiles * 100
        : 0;
}