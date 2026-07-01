using System.IO;
using WPF_Sorter_2._0.Core.Contracts.Services;
using WPF_Sorter_2._0.Core.Models;

namespace WPF_Sorter_2._0.Services;

public class FileSystemService : IFileSystemService
{
    public async Task<List<string>> GetFilesAsync(string folderPath, bool includeSubfolders, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            try
            {
                if (!Directory.Exists(folderPath))
                {
                    return new List<string>();
                }

                var searchOption = includeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                return Directory.GetFiles(folderPath, "*.*", searchOption).ToList();
            }
            catch (UnauthorizedAccessException)
            {
                // Нет доступа к папке
                return new List<string>();
            }
            catch (DirectoryNotFoundException)
            {
                // Папка не найдена
                return new List<string>();
            }
            catch (Exception)
            {
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
            // Проверяем существование исходного файла
            if (!File.Exists(sourcePath))
            {
                result.Success = false;
                result.ErrorMessage = "Исходный файл не найден";
                result.OperationType = FileOperationType.Failed;
                return result;
            }

            // Проверяем существование папки назначения
            if (!Directory.Exists(destinationFolder))
            {
                await EnsureFolderExistsAsync(destinationFolder, cancellationToken);
            }

            var fileName = Path.GetFileName(sourcePath);
            var destPath = Path.Combine(destinationFolder, fileName);

            // Обработка конфликтов имён
            if (!overwrite && File.Exists(destPath))
            {
                destPath = GetUniqueFilePath(destPath);
            }

            result.DestinationPath = destPath;

            // Выполняем перемещение
            await Task.Run(() =>
            {
                if (overwrite && File.Exists(destPath))
                {
                    File.Delete(destPath);
                }
                File.Move(sourcePath, destPath);
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
            // Проверяем существование исходного файла
            if (!File.Exists(sourcePath))
            {
                result.Success = false;
                result.ErrorMessage = "Исходный файл не найден";
                result.OperationType = FileOperationType.Failed;
                return result;
            }

            // Проверяем существование папки назначения
            if (!Directory.Exists(destinationFolder))
            {
                await EnsureFolderExistsAsync(destinationFolder, cancellationToken);
            }

            var fileName = Path.GetFileName(sourcePath);
            var destPath = Path.Combine(destinationFolder, fileName);

            // Обработка конфликтов имён
            if (!overwrite && File.Exists(destPath))
            {
                destPath = GetUniqueFilePath(destPath);
            }

            result.DestinationPath = destPath;

            // Выполняем копирование
            await Task.Run(() =>
            {
                if (overwrite && File.Exists(destPath))
                {
                    File.Delete(destPath);
                }
                File.Copy(sourcePath, destPath, overwrite);
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
        }

        return result;
    }

    public async Task EnsureFolderExistsAsync(string folderPath, CancellationToken cancellationToken = default)
    {
        await Task.Run(() =>
        {
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }
        }, cancellationToken);
    }

    public bool DirectoryExists(string path)
    {
        return Directory.Exists(path);
    }

    public bool FileExists(string path)
    {
        return File.Exists(path);
    }

    /// <summary>
    /// Создаёт уникальное имя файла при конфликте
    /// </summary>
    private string GetUniqueFilePath(string path)
    {
        var directory = Path.GetDirectoryName(path) ?? string.Empty;
        var fileNameWithoutExt = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);
        var counter = 1;

        while (File.Exists(path))
        {
            var newFileName = $"{fileNameWithoutExt} ({counter}){extension}";
            path = Path.Combine(directory, newFileName);
            counter++;
        }

        return path;
    }
}