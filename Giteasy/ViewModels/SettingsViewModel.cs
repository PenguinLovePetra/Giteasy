using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Giteasy.Helpers;
using Giteasy.Models;
using Giteasy.Services;
using Microsoft.UI.Xaml;
using Windows.Storage.Pickers;

namespace Giteasy.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly GitService _git;
    private readonly DatabaseService _db;
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

    [ObservableProperty]
    private string _selectedTheme = AppThemes.Light;

    [ObservableProperty]
    private string _selectedBackend = BackendModes.Builtin;

    [ObservableProperty]
    private bool _isGitExeAvailable;

    [ObservableProperty]
    private string _gitExePath = "";

    /// <summary>利用可能なテーマのリスト。</summary>
    public List<ThemeOption> AvailableThemes => AppThemes.AvailableThemes;

    /// <summary>バックエンド切替時に呼ばれるコールバック。</summary>
    public event Action<string>? BackendChanged;

    /// <summary>設定変更時に呼ばれるコールバック。</summary>
    public event Action? SettingsChanged;

    /// <summary>テーマ変更時に呼ばれるコールバック。</summary>
    public event Action<string>? ThemeChanged;

    public SettingsViewModel(GitService git, DatabaseService db)
    {
        _git = git;
        _db = db;
    }

    public void SetXamlRoot(XamlRoot root) => _xamlRoot = root;
    public void SetWindow(Window window) => _window = window;

    /// <summary>SettingsPage等からWindow参照を取得するためのプロパティ。</summary>
    public Window? Window => _window;

    /// <summary>DB から設定を読み込みます。</summary>
    public void LoadSettings()
    {
        try
        {
            RepositoryPath = _db.GetSetting("repositoryPath") ?? "";
            UserName = _db.GetSetting("userName") ?? "";
            UserEmail = _db.GetSetting("userEmail") ?? "";
            SelectedTheme = _db.GetSetting("theme") ?? AppThemes.Light;

            // git.exe 検出
            IsGitExeAvailable = GitExeDetector.IsAvailable;
            GitExePath = GitExeDetector.DetectedPath ?? "見つかりません";

            // バックエンド設定: DBに保存済みならそれを使用、未保存ならgit.exe優先
            var savedBackend = _db.GetSetting("gitBackend");
            if (savedBackend != null)
            {
                SelectedBackend = savedBackend;
            }
            else
            {
                // 初回起動: git.exeがあればsystemを優先（SSH認証等が自動利用可能）
                SelectedBackend = IsGitExeAvailable ? BackendModes.System : BackendModes.Builtin;
            }

            ApplySettingsToService();
        }
        catch
        {
            // 読み込み失敗時は続行
        }
    }

    [RelayCommand]
    private async Task BrowseRepositoryAsync()
    {
        if (_window == null) return;

        var picker = new FolderPicker();
        picker.SuggestedStartLocation = PickerLocationId.Desktop;
        picker.FileTypeFilter.Add("*");

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

            // DB に保存
            _db.SetSetting("repositoryPath", RepositoryPath);
            _db.SetSetting("userName", UserName);
            _db.SetSetting("userEmail", UserEmail);
            _db.SetSetting("theme", SelectedTheme);
            _db.SetSetting("gitBackend", SelectedBackend);

            IsRepositoryValid = _git.IsRepositorySet;
            StatusMessage = IsRepositoryValid
                ? "✓ 設定を保存しました。リポジトリは正常です。"
                : "⚠ 指定されたパスにGitリポジトリが見つかりません。";

            // テーマ変更通知
            ThemeChanged?.Invoke(SelectedTheme);
            // バックエンド変更通知
            BackendChanged?.Invoke(SelectedBackend);
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

        // バックエンド切替
        _git.SetBackend(SelectedBackend);
    }
}
