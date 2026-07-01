namespace WPF_Sorter_2._0.Core.Contracts.Services;

public interface IHashService
{
    /// <summary>
    /// Вычисляет SHA256 ХЭШ файла
    /// </summary>
    Task<string> ComputeHashAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Сравнивает два файла по ХЭШу
    /// </summary>
    Task<bool> CompareFilesByHashAsync(string file1, string file2, CancellationToken cancellationToken = default);

    /// <summary>
    /// Вычисляет ХЭШ с прогрессом
    /// </summary>
    Task<(string Hash, int Progress)> ComputeHashWithProgressAsync(
        string filePath,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default);
}