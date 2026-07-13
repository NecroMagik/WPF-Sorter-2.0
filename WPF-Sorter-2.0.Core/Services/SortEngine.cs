using System.Collections.Concurrent;
using WPF_Sorter_2._0.Core.Contracts.Services;
using WPF_Sorter_2._0.Core.Models;
using System.Threading.Tasks;

namespace WPF_Sorter_2._0.Core.Services;

public class SortEngine
{
    private readonly IFileSystemService _fileSystem;
    private readonly IHashService _hashService;
    private const int MAX_RETRIES = 3;
    private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(Math.Max(1, Environment.ProcessorCount / 2));

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
        var resultsLock = new object();

        foreach (var group in groupedFiles)
        {
            var rule = group.Key;
            var matchedFiles = group.Value;

            if (!rule.IsEnabled || string.IsNullOrEmpty(rule.DestinationFolder))
            {
                continue;
            }

            var destinationFolder = Path.Combine(sourceFolder, rule.DestinationFolder);
            await _fileSystem.EnsureFolderExistsAsync(destinationFolder, cancellationToken);

            // 👇 Создаём задачи для параллельной обработки
            var tasks = new List<Task>();

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

                // Ожидаем семафор перед запуском новой задачи
                await _semaphore.WaitAsync(cancellationToken);

                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        await ProcessFileAsync(
                            file,
                            destinationFolder,
                            profile,
                            totalFiles,
                            processed,
                            hashPassed,
                            hashFailed,
                            results,
                            resultsLock,
                            progress,
                            cancellationToken);
                    }
                    finally
                    {
                        _semaphore.Release();
                    }
                }, cancellationToken));
            }

            // Ожидаем завершения всех задач для этой категории
            await Task.WhenAll(tasks);
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

    private async Task ProcessFileAsync(
        string file,
        string destinationFolder,
        SortProfile profile,
        int totalFiles,
        int processed,
        int hashPassed,
        int hashFailed,
        List<FileOperationResult> results,
        object resultsLock,
        IProgress<SortProgress>? progress,
        CancellationToken cancellationToken)
    {
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
            if (result.Success && _fileSystem.FileExists(result.DestinationPath))
            {
                result.DestinationHash = await _hashService.ComputeHashAsync(result.DestinationPath, cancellationToken);
                result.HashVerified = string.Equals(result.SourceHash, result.DestinationHash, StringComparison.OrdinalIgnoreCase);

                if (result.HashVerified)
                {
                    Interlocked.Increment(ref hashPassed);
                }
                else
                {
                    Interlocked.Increment(ref hashFailed);
                    result.OperationType = FileOperationType.HashMismatch;
                    result.ErrorMessage = "ХЭШ-контроль не пройден! Файл повреждён!";

                    try
                    {
                        await TryRecoverFile(file, result.DestinationPath, cancellationToken);

                        // После восстановления проверяем ещё раз
                        if (_fileSystem.FileExists(result.DestinationPath))
                        {
                            var newHash = await _hashService.ComputeHashAsync(result.DestinationPath, cancellationToken);
                            result.HashVerified = string.Equals(result.SourceHash, newHash, StringComparison.OrdinalIgnoreCase);
                            if (result.HashVerified)
                            {
                                Interlocked.Decrement(ref hashFailed);
                                Interlocked.Increment(ref hashPassed);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"❌ Recovery failed: {ex.Message}");
                    }
                }
            }
            else if (!result.Success)
            {
                Interlocked.Increment(ref hashFailed);
                result.OperationType = FileOperationType.Failed;
            }

            result.ProcessingTime = DateTime.Now - startTime;

            lock (resultsLock)
            {
                results.Add(result);
            }

            var currentProcessed = Interlocked.Increment(ref processed);

            var progressReport = new SortProgress
            {
                TotalFiles = totalFiles,
                ProcessedFiles = currentProcessed,
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

            lock (resultsLock)
            {
                results.Add(result);
            }

            var currentProcessed = Interlocked.Increment(ref processed);
            progress?.Report(new SortProgress
            {
                TotalFiles = totalFiles,
                ProcessedFiles = currentProcessed,
                CurrentFile = fileName,
                CurrentStatus = $"❌ {ex.Message}"
            });
        }
    }

    private Dictionary<SortRule, List<string>> GroupFilesByRules(
        List<string> files,
        List<SortRule> rules,
        string defaultCategoryPath)
    {
        var result = new Dictionary<SortRule, List<string>>();

        foreach (var rule in rules.Where(r => r.IsEnabled))
        {
            result[rule] = new List<string>();
        }

        foreach (var file in files)
        {
            var extension = Path.GetExtension(file).ToLowerInvariant();

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
        int retryCount = 0;
        Exception? lastException = null;

        while (retryCount < MAX_RETRIES)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"🔄 Recovery attempt {retryCount + 1} for: {Path.GetFileName(sourceFile)}");

                // Удаляем повреждённый файл
                if (_fileSystem.FileExists(destinationFile))
                {
                    File.Delete(NormalizePath(destinationFile));
                }

                // Копируем заново
                File.Copy(NormalizePath(sourceFile), NormalizePath(destinationFile), true);

                // Проверяем размер
                var sourceInfo = new FileInfo(NormalizePath(sourceFile));
                var destInfo = new FileInfo(NormalizePath(destinationFile));

                if (sourceInfo.Length != destInfo.Length)
                {
                    throw new InvalidOperationException($"Размеры не совпадают: {sourceInfo.Length} vs {destInfo.Length}");
                }

                // Проверяем ХЭШ
                var sourceHash = await _hashService.ComputeHashAsync(sourceFile, cancellationToken);
                var destHash = await _hashService.ComputeHashAsync(destinationFile, cancellationToken);

                if (string.Equals(sourceHash, destHash, StringComparison.OrdinalIgnoreCase))
                {
                    System.Diagnostics.Debug.WriteLine($"✅ Recovery successful for: {Path.GetFileName(sourceFile)}");
                    return;
                }

                // Если ХЭШ не совпал - удаляем и пробуем снова
                if (_fileSystem.FileExists(destinationFile))
                {
                    File.Delete(NormalizePath(destinationFile));
                }

                lastException = new InvalidOperationException("ХЭШ не совпадает после восстановления");
                retryCount++;
            }
            catch (Exception ex)
            {
                lastException = ex;
                System.Diagnostics.Debug.WriteLine($"⚠️ Recovery attempt {retryCount + 1} failed: {ex.Message}");
                retryCount++;

                try
                {
                    if (_fileSystem.FileExists(destinationFile))
                    {
                        File.Delete(NormalizePath(destinationFile));
                    }
                }
                catch { }

                if (retryCount < MAX_RETRIES)
                {
                    await Task.Delay(100 * retryCount, cancellationToken);
                }
            }
        }

        System.Diagnostics.Debug.WriteLine($"❌ Recovery FAILED for: {Path.GetFileName(sourceFile)}");
        throw new InvalidOperationException(
            $"Не удалось восстановить файл после {MAX_RETRIES} попыток. " +
            $"Последняя ошибка: {lastException?.Message ?? "Неизвестная ошибка"}",
            lastException);
    }

    private string NormalizePath(string path)
    {
        if (string.IsNullOrEmpty(path)) return path;
        if (path.Length > 260 && !path.StartsWith(@"\\?\"))
        {
            if (path.StartsWith(@"\\"))
            {
                return @"\\?\UNC\" + path.Substring(2);
            }
            return @"\\?\" + path;
        }
        return path;
    }
}