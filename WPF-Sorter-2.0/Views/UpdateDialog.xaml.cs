using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Input;
using MahApps.Metro.Controls;
using WPF_Sorter_2._0.Core.Models;

namespace WPF_Sorter_2._0.Views;

public partial class UpdateDialog : MetroWindow
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, DwmWindowAttribute dwAttribute, ref int pvAttribute, int cbAttribute);

    private enum DwmWindowAttribute
    {
        CornerPreference = 33
    }

    public UpdateInfo UpdateInfo { get; }
    public string CurrentVersion { get; }
    public string NewVersion => UpdateInfo.Version;
    public string Changelog => UpdateInfo.Changelog;
    public string AssetInfo { get; }

    public ICommand InstallCommand { get; }
    public ICommand CancelCommand { get; }

    public bool IsInstallConfirmed { get; private set; }

    public UpdateDialog(UpdateInfo updateInfo, string currentVersion)
    {
        InitializeComponent();
        DataContext = this;

        UpdateInfo = updateInfo;
        CurrentVersion = currentVersion;

        // Формируем информацию о файле
        var sizeMB = updateInfo.AssetSize > 0 ? (updateInfo.AssetSize / 1024.0 / 1024.0).ToString("F1") : "?";
        var isExe = updateInfo.AssetName?.EndsWith(".exe") == true;
        var typeName = isExe ? "Single EXE (самодостаточный)" : "Portable ZIP";
        var description = isExe
            ? "Один файл, не требует установки .NET Runtime"
            : "Распакуйте и запустите, требует .NET 8.0 Runtime";

        AssetInfo = $"{typeName}\n{description}\nРазмер: {sizeMB} MB";

        InstallCommand = new RelayCommand(() =>
        {
            IsInstallConfirmed = true;
            DialogResult = true;
            Close();
        });

        CancelCommand = new RelayCommand(() =>
        {
            DialogResult = false;
            Close();
        });

        SourceInitialized += OnSourceInitialized;
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

    private class RelayCommand : ICommand
    {
        private readonly Action _execute;

        public RelayCommand(Action execute)
        {
            _execute = execute;
        }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => _execute();
    }
}