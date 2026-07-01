using System.Collections.Concurrent;
using WPF_Sorter_2._0.Core.Contracts.Services;
using WPF_Sorter_2._0.Core.Models;

namespace WPF_Sorter_2._0.Core.Services;

public class SortEngine
{
    private readonly IFileSystemService _fileSystem;
    private readonly IHashService _hashService;

    public SortEngine(IFileSystemService fileSystem, IHashService hashService)
    {
        _fileSystem = fileSystem;
        _hashService = hashService;
    }

    public async Task<List<FileOperationResult>> ExecuteSortAsync(
        string sourceFolder,
        SortProfile profile,
        IProgress<SortProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var results = new List<FileOperationResult>();

        // 1. Получаем все файлы
        var files = await _fileSystem.GetFilesAsync(sourceFolder, profile.IncludeSubfolders, cancellationToken);
        var totalFiles = files.Count;

        if (totalFiles == 0)
        {
            progress?.Report(new SortProgress
            {
                TotalFiles = 0,
                ProcessedFiles = 0,
                CurrentStatus = "⚠️ Файлы не найдены"
            });
            return results;
        }

        progress?.Report(new SortProgress
        {
            TotalFiles = totalFiles,
            ProcessedFiles = 0,
            CurrentStatus = $"🔍 Найдено файлов: {totalFiles}"
        });

        // 2. Группируем файлы по правилам
        var groupedFiles = GroupFilesByRules(files, profile.Rules, profile.DefaultCategoryPath);
        var processed = 0;
        var hashPassed = 0;
        var hashFailed = 0;

        foreach (var group in groupedFiles)
        {
            var rule = group.Key;
            var matchedFiles = group.Value;

            if (!rule.IsEnabled || string.IsNullOrEmpty(rule.DestinationFolder))
            {
                // Пропускаем выключенные категории или без папки
                continue;
            }

            // Создаём папку назначения
            var destinationFolder = Path.Combine(sourceFolder, rule.DestinationFolder);
            await _fileSystem.EnsureFolderExistsAsync(destinationFolder, cancellationToken);

            foreach (var file in matchedFiles)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    progress?.Report(new SortProgress
                    {
                        TotalFiles = totalFiles,
                        ProcessedFiles = processed,
                        CurrentStatus = "⏹️ Операция отменена"
                    });
                    return results;
                }

                var fileName = Path.GetFileName(file);
                var destPath = Path.Combine(destinationFolder, fileName);
                var result = new FileOperationResult
                {
                    SourcePath = file,
                    DestinationPath = destPath,
                    FileSize = new FileInfo(file).Length
                };

                var startTime = DateTime.Now;

                try
                {
                    // 1. Вычисляем ХЭШ исходного файла
                    result.SourceHash = await _hashService.ComputeHashAsync(file, cancellationToken);

                    // 2. Выполняем перемещение/копирование
                    if (profile.MoveInsteadOfCopy)
                    {
                        // Перемещаем файл
                        var moveResult = await _fileSystem.MoveFileAsync(
                            file,
                            destinationFolder,
                            profile.OverwriteExisting,
                            cancellationToken);

                        result.Success = moveResult.Success;
                        result.ErrorMessage = moveResult.ErrorMessage;
                        result.OperationType = moveResult.Success ? FileOperationType.Moved : FileOperationType.Failed;
                        result.DestinationPath = moveResult.DestinationPath ?? destPath;
                    }
                    else
                    {
                        // Копируем файл
                        var copyResult = await _fileSystem.CopyFileAsync(
                            file,
                            destinationFolder,
                            profile.OverwriteExisting,
                            cancellationToken);

                        result.Success = copyResult.Success;
                        result.ErrorMessage = copyResult.ErrorMessage;
                        result.OperationType = copyResult.Success ? FileOperationType.Copied : FileOperationType.Failed;
                        result.DestinationPath = copyResult.DestinationPath ?? destPath;
                    }

                    // 3. Если операция успешна - проверяем ХЭШ
                    if (result.Success && File.Exists(result.DestinationPath))
                    {
                        // Вычисляем ХЭШ нового файла
                        result.DestinationHash = await _hashService.ComputeHashAsync(result.DestinationPath, cancellationToken);

                        // Сравниваем ХЭШи
                        result.HashVerified = string.Equals(result.SourceHash, result.DestinationHash, StringComparison.OrdinalIgnoreCase);

                        if (result.HashVerified)
                        {
                            hashPassed++;
                        }
                        else
                        {
                            hashFailed++;
                            result.OperationType = FileOperationType.HashMismatch;
                            result.ErrorMessage = "ХЭШ-контроль не пройден! Файл повреждён!";

                            // Если ХЭШ не совпал - пытаемся восстановить
                            await TryRecoverFile(file, result.DestinationPath, cancellationToken);
                        }
                    }
                    else if (!result.Success)
                    {
                        // Если файл не переместился/скопировался
                        hashFailed++;
                        result.OperationType = FileOperationType.Failed;
                    }

                    result.ProcessingTime = DateTime.Now - startTime;
                    results.Add(result);

                    processed++;
                    var progressReport = new SortProgress
                    {
                        TotalFiles = totalFiles,
                        ProcessedFiles = processed,
                        CurrentFile = fileName,
                        HashCheckPassed = hashPassed,
                        HashCheckFailed = hashFailed,
                        CurrentStatus = result.HashVerified
                            ? $"✅ {fileName}"
                            : $"❌ {fileName} - Ошибка ХЭШа!"
                    };
                    progress?.Report(progressReport);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.ErrorMessage = ex.Message;
                    result.OperationType = FileOperationType.Failed;
                    result.ProcessingTime = DateTime.Now - startTime;
                    results.Add(result);
                    processed++;

                    progress?.Report(new SortProgress
                    {
                        TotalFiles = totalFiles,
                        ProcessedFiles = processed,
                        CurrentFile = fileName,
                        CurrentStatus = $"❌ {ex.Message}"
                    });
                }
            }
        }

        // Итоговый статус
        progress?.Report(new SortProgress
        {
            TotalFiles = totalFiles,
            ProcessedFiles = processed,
            HashCheckPassed = hashPassed,
            HashCheckFailed = hashFailed,
            CurrentStatus = $"✅ Готово! Обработано: {processed} файлов. ХЭШ-проверка: {hashPassed} прошло, {hashFailed} не прошло"
        });

        return results;
    }

    private Dictionary<SortRule, List<string>> GroupFilesByRules(
        List<string> files,
        List<SortRule> rules,
        string defaultCategoryPath)
    {
        var result = new Dictionary<SortRule, List<string>>();

        // Инициализируем словарь
        foreach (var rule in rules.Where(r => r.IsEnabled))
        {
            result[rule] = new List<string>();
        }

        // Группируем файлы
        foreach (var file in files)
        {
            var extension = Path.GetExtension(file).ToLowerInvariant();
            var fileName = Path.GetFileName(file);

            var matchedRule = rules.FirstOrDefault(r =>
                r.IsEnabled &&
                r.Extensions.Any(ext => string.Equals(ext, extension, StringComparison.OrdinalIgnoreCase)));

            if (matchedRule != null)
            {
                result[matchedRule].Add(file);
            }
        }

        return result;
    }

    private async Task TryRecoverFile(string sourceFile, string destinationFile, CancellationToken cancellationToken)
    {
        try
        {
            // Если ХЭШ не совпал - удаляем повреждённый файл и пытаемся скопировать заново
            if (File.Exists(destinationFile))
            {
                File.Delete(destinationFile);
            }

            // Копируем заново без проверки
            File.Copy(sourceFile, destinationFile, true);

            // Проверяем ХЭШ ещё раз
            var destHash = await _hashService.ComputeHashAsync(destinationFile, cancellationToken);
            var sourceHash = await _hashService.ComputeHashAsync(sourceFile, cancellationToken);

            if (!string.Equals(sourceHash, destHash, StringComparison.OrdinalIgnoreCase))
            {
                // Если всё равно не совпало - помечаем как критическую ошибку
                File.Delete(destinationFile);
            }
        }
        catch
        {
            // Игнорируем ошибки восстановления
        }
    }
}