using WPF_Sorter_2._0.Core.Models;
using WPF_Sorter_2._0.Core.Services;
using WPF_Sorter_2._0.ViewModels;

namespace WPF_Sorter_2._0.Services;

public class SortBackgroundService
{
    private readonly SortEngine _sortEngine;
    private readonly SortSetViewModel _sortSetVM;

    private CancellationTokenSource? _cancellationTokenSource;
    private Task<List<FileOperationResult>>? _currentSortTask;

    public event EventHandler<SortProgress>? ProgressUpdated;
    public event EventHandler<List<FileOperationResult>>? SortCompleted;
    public event EventHandler<string>? SortFailed;
    public event EventHandler? SortStarted;
    public event EventHandler? SortCancelled;

    public bool IsSorting { get; private set; }
    public SortProgress? CurrentProgress { get; private set; }
    public List<FileOperationResult>? LastResults { get; private set; }

    public string? CurrentSourceFolder { get; private set; }
    public bool CurrentIncludeSubfolders { get; private set; }

    public SortBackgroundService(SortEngine sortEngine, SortSetViewModel sortSetVM)
    {
        _sortEngine = sortEngine;
        _sortSetVM = sortSetVM;
    }

    public async Task StartSortingAsync(string sourceFolder, bool includeSubfolders)
    {
        if (IsSorting)
        {
            return;
        }

        // Проверяем наличие активных категорий
        var categories = _sortSetVM.Categories.Where(c => c.IsEnabled).ToList();
        if (categories.Count == 0)
        {
            SortFailed?.Invoke(this, "Нет активных категорий для сортировки");
            return;
        }

        // Создаём профиль сортировки
        var profile = new SortProfile
        {
            Name = "Основной профиль",
            SourceFolder = sourceFolder,
            Rules = categories.Select(c => new SortRule
            {
                Name = c.Name,
                IconGlyph = c.IconGlyph,
                Extensions = c.Extensions,
                DestinationFolder = c.CustomPath,
                IsEnabled = c.IsEnabled
            }).ToList(),
            IncludeSubfolders = includeSubfolders,
            MoveInsteadOfCopy = true,
            OverwriteExisting = false
        };

        // Создаём токен отмены
        _cancellationTokenSource = new CancellationTokenSource();
        var token = _cancellationTokenSource.Token;

        IsSorting = true;
        CurrentProgress = null;
        LastResults = null;
        SortStarted?.Invoke(this, EventArgs.Empty);

        try
        {
            var progress = new Progress<SortProgress>(p =>
            {
                CurrentProgress = p;
                ProgressUpdated?.Invoke(this, p);
            });

            _currentSortTask = _sortEngine.ExecuteSortAsync(
                sourceFolder,
                profile,
                progress,
                token);

            var results = await _currentSortTask;
            LastResults = results;
            SortCompleted?.Invoke(this, results);
        }
        catch (OperationCanceledException)
        {
            SortCancelled?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            SortFailed?.Invoke(this, ex.Message);
        }
        finally
        {
            IsSorting = false;
            _currentSortTask = null;
        }
    }

    public void CancelSorting()
    {
        if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
        {
            _cancellationTokenSource.Cancel();
        }
    }

    public async Task WaitForCompletionAsync()
    {
        if (_currentSortTask != null)
        {
            await _currentSortTask;
        }
    }

    public void Reset()
    {
        if (!IsSorting)
        {
            CurrentProgress = null;
            LastResults = null;
        }
    }
}