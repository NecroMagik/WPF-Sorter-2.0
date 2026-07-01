using System.Security.Cryptography;
using WPF_Sorter_2._0.Core.Contracts.Services;

namespace WPF_Sorter_2._0.Core.Services;

public class HashService : IHashService
{
    private readonly int _bufferSize = 8192; // 8KB буфер для оптимизации

    public async Task<string> ComputeHashAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            throw new FileNotFoundException($"File not found: {filePath}");
        }

        try
        {
            using var stream = File.OpenRead(filePath);
            using var sha256 = SHA256.Create();

            // Читаем файл с буфером для производительности
            var buffer = new byte[_bufferSize];
            int bytesRead;

            while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
            {
                // SHA256 обновляется в реальном времени
                sha256.TransformBlock(buffer, 0, bytesRead, buffer, 0);
                cancellationToken.ThrowIfCancellationRequested();
            }

            sha256.TransformFinalBlock(buffer, 0, 0);

            // Преобразуем в hex строку
            return BitConverter.ToString(sha256.Hash ?? Array.Empty<byte>())
                .Replace("-", "")
                .ToLowerInvariant();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to compute hash for {filePath}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Быстрое сравнение двух файлов по ХЭШу
    /// </summary>
    public async Task<bool> CompareFilesByHashAsync(string file1, string file2, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(file1) || !File.Exists(file2))
            return false;

        // Сначала сравниваем размеры (быстрая проверка)
        var file1Size = new FileInfo(file1).Length;
        var file2Size = new FileInfo(file2).Length;

        if (file1Size != file2Size)
            return false;

        // Если размеры совпадают, сравниваем ХЭШи
        var hash1 = await ComputeHashAsync(file1, cancellationToken);
        var hash2 = await ComputeHashAsync(file2, cancellationToken);

        return string.Equals(hash1, hash2, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Вычисление ХЭШа файла с прогрессом (для больших файлов)
    /// </summary>
    public async Task<(string Hash, int Progress)> ComputeHashWithProgressAsync(
        string filePath,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            throw new FileNotFoundException($"File not found: {filePath}");
        }

        var fileInfo = new FileInfo(filePath);
        var totalBytes = fileInfo.Length;
        var bytesRead = 0L;
        var lastProgress = 0;

        using var stream = File.OpenRead(filePath);
        using var sha256 = SHA256.Create();

        var buffer = new byte[_bufferSize];
        int bytesReadThisChunk;

        while ((bytesReadThisChunk = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
        {
            sha256.TransformBlock(buffer, 0, bytesReadThisChunk, buffer, 0);
            bytesRead += bytesReadThisChunk;
            cancellationToken.ThrowIfCancellationRequested();

            // Обновляем прогресс
            var currentProgress = (int)((double)bytesRead / totalBytes * 100);
            if (currentProgress != lastProgress && currentProgress % 5 == 0)
            {
                progress?.Report(currentProgress);
                lastProgress = currentProgress;
            }
        }

        sha256.TransformFinalBlock(buffer, 0, 0);

        var hash = BitConverter.ToString(sha256.Hash ?? Array.Empty<byte>())
            .Replace("-", "")
            .ToLowerInvariant();

        progress?.Report(100);

        return (hash, 100);
    }
}