using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Giteasy.Helpers;
using Giteasy.Services;
using Microsoft.UI.Xaml;
using Windows.Storage.Pickers;

namespace Giteasy.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly GitService _git;
    private XamlRoot? _xamlRoot;
    private Window? _window;

    [ObservableProperty]
    private string _repositoryPath = "";

    [ObservableProperty]
    private string _userName = "";

    [ObservableProperty]
    private string _userEmail = "";

    [ObservableProperty]
    private string _statusMessage = "";

    [ObservableProperty]
    private bool _isRepositoryValid;

    /// <summary>設定変更時に呼ばれるコールバック。</summary>
    public event Action? SettingsChanged;

    private static readonly string SettingsFilePath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GitEasy", "settings.json");

    public SettingsViewModel(GitService git)
    {
        _git = git;
    }

    public void SetXamlRoot(XamlRoot root) => _xamlRoot = root;
    public void SetWindow(Window window) => _window = window;

    /// <summary>保存された設定を読み込みます。</summary>
    public void LoadSettings()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                var json = File.ReadAllText(SettingsFilePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                if (settings != null)
                {
                    RepositoryPath = settings.RepositoryPath ?? "";
                    UserName = settings.UserName ?? "";
                    UserEmail = settings.UserEmail ?? "";

                    ApplySettingsToService();
                }
            }
        }
        catch
        {
            // 設定ファイルの読み込みに失敗しても続行
        }
    }

    [RelayCommand]
    private async Task BrowseRepositoryAsync()
    {
        if (_window == null) return;

        var picker = new FolderPicker();
        picker.SuggestedStartLocation = PickerLocationId.Desktop;
        picker.FileTypeFilter.Add("*");

        // WinUI 3 ではウィンドウハンドルが必要
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(_window);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        var folder = await picker.PickSingleFolderAsync();
        if (folder != null)
        {
            RepositoryPath = folder.Path;
        }
    }

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        if (_xamlRoot == null) return;

        try
        {
            ApplySettingsToService();

            // 設定ファイルの保存
            var dir = Path.GetDirectoryName(SettingsFilePath)!;
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var settings = new AppSettings
            {
                RepositoryPath = RepositoryPath,
                UserName = UserName,
                UserEmail = UserEmail
            };
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(SettingsFilePath, json);

            IsRepositoryValid = _git.IsRepositorySet;
            StatusMessage = IsRepositoryValid
                ? "✓ 設定を保存しました。リポジトリは正常です。"
                : "⚠ 指定されたパスにGitリポジトリが見つかりません。";

            SettingsChanged?.Invoke();

            await DialogHelper.ShowInfoAsync(_xamlRoot, "保存完了", "設定を保存しました。");
        }
        catch (Exception ex)
        {
            await DialogHelper.ShowErrorAsync(_xamlRoot, "設定の保存に失敗", ex.Message);
        }
    }

    private void ApplySettingsToService()
    {
        if (!string.IsNullOrWhiteSpace(RepositoryPath))
        {
            try
            {
                _git.SetRepository(RepositoryPath);
                IsRepositoryValid = true;
            }
            catch
            {
                IsRepositoryValid = false;
            }
        }

        if (!string.IsNullOrWhiteSpace(UserName) && !string.IsNullOrWhiteSpace(UserEmail))
        {
            _git.SetUser(UserName, UserEmail);
        }
    }

    private class AppSettings
    {
        public string? RepositoryPath { get; set; }
        public string? UserName { get; set; }
        public string? UserEmail { get; set; }
    }
}
