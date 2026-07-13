using System.IO;
using WPF_Sorter_2._0.Core.Contracts.Services;
using WPF_Sorter_2._0.Core.Models;

namespace WPF_Sorter_2._0.Services;

public class FileSystemService : IFileSystemService
{
    // 👇 Поддержка длинных путей (>260 символов)
    private const int MAX_PATH = 260;

    private string NormalizePath(string path)
    {
        if (string.IsNullOrEmpty(path)) return path;

        // Если путь длиннее MAX_PATH, добавляем префикс \\?\
        if (path.Length > MAX_PATH && !path.StartsWith(@"\\?\"))
        {
            if (path.StartsWith(@"\\"))
            {
                return @"\\?\UNC\" + path.Substring(2);
            }
            return @"\\?\" + path;
        }
        return path;
    }

    public async Task<List<string>> GetFilesAsync(string folderPath, bool includeSubfolders, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            try
            {
                var normalizedPath = NormalizePath(folderPath);

                if (!Directory.Exists(normalizedPath))
                {
                    return new List<string>();
                }

                var searchOption = includeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                var files = new List<string>();

                foreach (var file in Directory.EnumerateFiles(normalizedPath, "*.*", searchOption))
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    // Возвращаем оригинальный путь
                    var originalPath = file;
                    if (file.StartsWith(@"\\?\"))
                    {
                        originalPath = file.Substring(4);
                        if (originalPath.StartsWith(@"UNC\"))
                        {
                            originalPath = @"\\" + originalPath.Substring(4);
                        }
                    }
                    files.Add(originalPath);
                }

                return files;
            }
            catch (UnauthorizedAccessException ex)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ No access to folder: {ex.Message}");
                return new List<string>();
            }
            catch (DirectoryNotFoundException ex)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ Folder not found: {ex.Message}");
                return new List<string>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ GetFiles error: {ex.Message}");
                return new List<string>();
            }
        }, cancellationToken);
    }

    public async Task<FileOperationResult> MoveFileAsync(
        string sourcePath,
        string destinationFolder,
        bool overwrite,
        CancellationToken cancellationToken = default)
    {
        var result = new FileOperationResult
        {
            SourcePath = sourcePath,
            OperationType = FileOperationType.Moved
        };

        try
        {
            var normalizedSource = NormalizePath(sourcePath);

            if (!File.Exists(normalizedSource))
            {
                result.Success = false;
                result.ErrorMessage = "Исходный файл не найден";
                result.OperationType = FileOperationType.Failed;
                return result;
            }

            var normalizedDestFolder = NormalizePath(destinationFolder);
            await EnsureFolderExistsAsync(normalizedDestFolder, cancellationToken);

            var fileName = Path.GetFileName(sourcePath);
            var destPath = Path.Combine(destinationFolder, fileName);
            var normalizedDest = NormalizePath(destPath);

            // Обработка конфликтов имён
            if (!overwrite && File.Exists(normalizedDest))
            {
                destPath = GetUniqueFilePath(destPath);
                normalizedDest = NormalizePath(destPath);
            }

            result.DestinationPath = destPath;

            await Task.Run(() =>
            {
                if (overwrite && File.Exists(normalizedDest))
                {
                    File.Delete(normalizedDest);
                }
                File.Move(normalizedSource, normalizedDest);
            }, cancellationToken);

            result.Success = true;
            result.OperationType = FileOperationType.Moved;
        }
        catch (OperationCanceledException)
        {
            result.Success = false;
            result.ErrorMessage = "Операция отменена";
            result.OperationType = FileOperationType.Failed;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.OperationType = FileOperationType.Failed;
            System.Diagnostics.Debug.WriteLine($"❌ MoveFile error: {ex.Message}");
        }

        return result;
    }

    public async Task<FileOperationResult> CopyFileAsync(
        string sourcePath,
        string destinationFolder,
        bool overwrite,
        CancellationToken cancellationToken = default)
    {
        var result = new FileOperationResult
        {
            SourcePath = sourcePath,
            OperationType = FileOperationType.Copied
        };

        try
        {
            var normalizedSource = NormalizePath(sourcePath);

            if (!File.Exists(normalizedSource))
            {
                result.Success = false;
                result.ErrorMessage = "Исходный файл не найден";
                result.OperationType = FileOperationType.Failed;
                return result;
            }

            var normalizedDestFolder = NormalizePath(destinationFolder);
            await EnsureFolderExistsAsync(normalizedDestFolder, cancellationToken);

            var fileName = Path.GetFileName(sourcePath);
            var destPath = Path.Combine(destinationFolder, fileName);
            var normalizedDest = NormalizePath(destPath);

            if (!overwrite && File.Exists(normalizedDest))
            {
                destPath = GetUniqueFilePath(destPath);
                normalizedDest = NormalizePath(destPath);
            }

            result.DestinationPath = destPath;

            await Task.Run(() =>
            {
                if (overwrite && File.Exists(normalizedDest))
                {
                    File.Delete(normalizedDest);
                }
                File.Copy(normalizedSource, normalizedDest, overwrite);
            }, cancellationToken);

            result.Success = true;
            result.OperationType = FileOperationType.Copied;
        }
        catch (OperationCanceledException)
        {
            result.Success = false;
            result.ErrorMessage = "Операция отменена";
            result.OperationType = FileOperationType.Failed;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.OperationType = FileOperationType.Failed;
            System.Diagnostics.Debug.WriteLine($"❌ CopyFile error: {ex.Message}");
        }

        return result;
    }

    public async Task EnsureFolderExistsAsync(string folderPath, CancellationToken cancellationToken = default)
    {
        await Task.Run(() =>
        {
            var normalizedPath = NormalizePath(folderPath);
            if (!Directory.Exists(normalizedPath))
            {
                Directory.CreateDirectory(normalizedPath);
            }
        }, cancellationToken);
    }

    public bool DirectoryExists(string path)
    {
        try
        {
            return Directory.Exists(NormalizePath(path));
        }
        catch
        {
            return false;
        }
    }

    public bool FileExists(string path)
    {
        try
        {
            return File.Exists(NormalizePath(path));
        }
        catch
        {
            return false;
        }
    }

    private string GetUniqueFilePath(string path)
    {
        var directory = Path.GetDirectoryName(path) ?? string.Empty;
        var fileNameWithoutExt = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);
        var counter = 1;
        var normalizedPath = NormalizePath(path);

        while (File.Exists(normalizedPath))
        {
            var newFileName = $"{fileNameWithoutExt} ({counter}){extension}";
            path = Path.Combine(directory, newFileName);
            normalizedPath = NormalizePath(path);
            counter++;
        }

        return path;
    }
}