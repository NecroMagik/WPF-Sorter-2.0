#nullable enable

using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Input;
using System.ComponentModel;
using MahApps.Metro.Controls;
using WPF_Sorter_2._0.Core.Models;
using WPF_Sorter_2._0.Services;

namespace WPF_Sorter_2._0.Views;

public partial class UpdateDialog : MetroWindow, INotifyPropertyChanged
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, DwmWindowAttribute dwAttribute, ref int pvAttribute, int cbAttribute);

    private enum DwmWindowAttribute
    {
        CornerPreference = 33
    }

    private readonly UpdateService _updateService;
    private bool _isInstallConfirmed = false;
    private CancellationTokenSource? _cancellationTokenSource;

    public UpdateInfo UpdateInfo { get; }
    public string CurrentVersion { get; }
    public string NewVersion => UpdateInfo.Version;
    public string Changelog => UpdateInfo.Changelog;
    public string AssetInfo { get; }

    private int _downloadProgress;
    public int DownloadProgress
    {
        get => _downloadProgress;
        set { _downloadProgress = value; OnPropertyChanged(nameof(DownloadProgress)); }
    }

    private bool _isDownloading;
    public bool IsDownloading
    {
        get => _isDownloading;
        set { _isDownloading = value; OnPropertyChanged(nameof(IsDownloading)); }
    }

    private bool _isBackgroundDownloading;
    public bool IsBackgroundDownloading
    {
        get => _isBackgroundDownloading;
        set { _isBackgroundDownloading = value; OnPropertyChanged(nameof(IsBackgroundDownloading)); }
    }

    private string _downloadStatus = string.Empty;
    public string DownloadStatus
    {
        get => _downloadStatus;
        set { _downloadStatus = value; OnPropertyChanged(nameof(DownloadStatus)); }
    }

    private string _backgroundDownloadStatus = string.Empty;
    public string BackgroundDownloadStatus
    {
        get => _backgroundDownloadStatus;
        set { _backgroundDownloadStatus = value; OnPropertyChanged(nameof(BackgroundDownloadStatus)); }
    }

    public ICommand InstallCommand { get; private set; }
    public ICommand BackgroundDownloadCommand { get; private set; }
    public ICommand CancelCommand { get; }

    public bool IsInstallConfirmed => _isInstallConfirmed;

    public UpdateDialog(UpdateInfo updateInfo, string currentVersion, UpdateService updateService)
    {
        InitializeComponent();
        DataContext = this;

        UpdateInfo = updateInfo;
        CurrentVersion = currentVersion;
        _updateService = updateService;

        var sizeMB = updateInfo.AssetSize > 0 ? (updateInfo.AssetSize / 1024.0 / 1024.0).ToString("F1") : "?";
        var isExe = updateInfo.AssetName?.EndsWith(".exe") == true;
        var typeName = isExe ? "Single EXE (самодостаточный)" : "Portable ZIP";
        var description = isExe
            ? "Один файл, не требует установки .NET Runtime"
            : "Распакуйте и запустите, требует .NET 8.0 Runtime";

        AssetInfo = $"{typeName}\n{description}\nРазмер: {sizeMB} MB";

        InstallCommand = new RelayCommand(DownloadAndInstallAsync);
        BackgroundDownloadCommand = new RelayCommand(BackgroundDownloadAsync);
        CancelCommand = new RelayCommand(() =>
        {
            _cancellationTokenSource?.Cancel();
            DialogResult = false;
            Close();
        });

        SourceInitialized += OnSourceInitialized;
    }

    private async Task DownloadAndInstallAsync()
    {
        if (IsDownloading) return;

        _cancellationTokenSource = new CancellationTokenSource();
        IsDownloading = true;
        DownloadStatus = "Начинаем загрузку...";
        DownloadProgress = 0;

        try
        {
            var progress = new Progress<int>(p =>
            {
                DownloadProgress = p;
                DownloadStatus = $"Загрузка: {p}%";
            });

            var filePath = await _updateService.DownloadUpdateAsync(UpdateInfo, progress);

            if (_cancellationTokenSource.Token.IsCancellationRequested)
            {
                DownloadStatus = "⏹️ Загрузка отменена";
                IsDownloading = false;
                return;
            }

            DownloadStatus = "✅ Загрузка завершена! Установка...";
            await Task.Delay(500);

            _isInstallConfirmed = true;
            DialogResult = true;
            Close();

            _updateService.InstallUpdate(filePath);
        }
        catch (OperationCanceledException)
        {
            DownloadStatus = "⏹️ Загрузка отменена";
            IsDownloading = false;
        }
        catch (Exception ex)
        {
            DownloadStatus = $"❌ Ошибка: {ex.Message}";
            IsDownloading = false;
        }
    }

    private async Task BackgroundDownloadAsync()
    {
        if (IsDownloading) return;

        _cancellationTokenSource = new CancellationTokenSource();
        IsBackgroundDownloading = true;
        BackgroundDownloadStatus = "⏳ Загрузка в фоне...";

        try
        {
            var progress = new Progress<int>(p =>
            {
                BackgroundDownloadStatus = $"Загрузка: {p}%";
            });

            var filePath = await _updateService.DownloadUpdateAsync(UpdateInfo, progress);

            if (_cancellationTokenSource.Token.IsCancellationRequested)
            {
                BackgroundDownloadStatus = "⏹️ Загрузка отменена";
                IsBackgroundDownloading = false;
                return;
            }

            BackgroundDownloadStatus = "✅ Загрузка завершена! Файл сохранён.";
            IsBackgroundDownloading = false;
        }
        catch (OperationCanceledException)
        {
            BackgroundDownloadStatus = "⏹️ Загрузка отменена";
            IsBackgroundDownloading = false;
        }
        catch (Exception ex)
        {
            BackgroundDownloadStatus = $"❌ Ошибка: {ex.Message}";
            IsBackgroundDownloading = false;
        }
    }

    private void OnSourceInitialized(object sender, EventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        TryEnableRoundCorners(hwnd);
    }

    private void TryEnableRoundCorners(IntPtr hwnd)
    {
        try
        {
            var cornerPreference = 2;
            DwmSetWindowAttribute(hwnd, DwmWindowAttribute.CornerPreference, ref cornerPreference, sizeof(int));
        }
        catch { }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        _cancellationTokenSource?.Cancel();
        base.OnClosing(e);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private class RelayCommand : ICommand
    {
        private readonly Func<Task>? _asyncExecute;
        private readonly Action? _syncExecute;

        public RelayCommand(Func<Task> execute)
        {
            _asyncExecute = execute;
        }

        public RelayCommand(Action execute)
        {
            _syncExecute = execute;
        }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => true;

        public async void Execute(object? parameter)
        {
            try
            {
                if (_asyncExecute != null)
                    await _asyncExecute();
                else
                    _syncExecute?.Invoke();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ RelayCommand error: {ex.Message}");
            }
        }

        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}