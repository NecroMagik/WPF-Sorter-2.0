using WPF_Sorter_2._0.Core.Models;

namespace WPF_Sorter_2._0.Core.Contracts.Services;

public interface IFileSystemService
{
    Task<List<string>> GetFilesAsync(string folderPath, bool includeSubfolders, CancellationToken cancellationToken = default);

    Task<FileOperationResult> MoveFileAsync(string sourcePath, string destinationFolder, bool overwrite, CancellationToken cancellationToken = default);

    Task<FileOperationResult> CopyFileAsync(string sourcePath, string destinationFolder, bool overwrite, CancellationToken cancellationToken = default);

    Task EnsureFolderExistsAsync(string folderPath, CancellationToken cancellationToken = default);

    bool DirectoryExists(string path);
    bool FileExists(string path);
}