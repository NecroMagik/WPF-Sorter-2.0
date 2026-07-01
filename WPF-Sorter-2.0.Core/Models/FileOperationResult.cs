namespace WPF_Sorter_2._0.Core.Models;

public class FileOperationResult
{
    public string SourcePath { get; set; } = string.Empty;
    public string DestinationPath { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public FileOperationType OperationType { get; set; }
    public string SourceHash { get; set; } = string.Empty;
    public string DestinationHash { get; set; } = string.Empty;
    public bool HashVerified { get; set; }
    public long FileSize { get; set; }
    public TimeSpan ProcessingTime { get; set; }
}

public enum FileOperationType
{
    Copied,
    Moved,
    Skipped,
    Failed,
    HashMismatch
}