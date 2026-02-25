using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Giteasy.Helpers;
using Giteasy.Services;
using Microsoft.UI.Xaml;
using Windows.Storage.Pickers;

namespace Giteasy.ViewModels;

public partial class RepoSetupViewModel : ObservableObject
{
    private readonly GitService _git;
    private XamlRoot? _xamlRoot;
    private Window? _window;

    // ─── 新規作成タブ ──────────────────

    [ObservableProperty]
    private string _initLocalPath = "";

    [ObservableProperty]
    private string _initRemoteUrl = "";

    // ─── クローンタブ ──────────────────

    [ObservableProperty]
    private string _cloneRemoteUrl = "";

    [ObservableProperty]
    private string _cloneLocalPath = "";

    // ─── 共通 ──────────────────────────

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = "";

    [ObservableProperty]
    private bool _autoInitBare = true;

    /// <summary>セットアップ完了時に呼ばれるコールバック。</summary>
    public event Action? SetupCompleted;

    public RepoSetupViewModel(GitService git)
    {
        _git = git;
    }

    public void SetXamlRoot(XamlRoot root) => _xamlRoot = root;
    public void SetWindow(Window window) => _window = window;

    // ─── フォルダ選択 ──────────────────

    [RelayCommand]
    private async Task BrowseInitPathAsync()
    {
        var path = await PickFolderAsync();
        if (path != null) InitLocalPath = path;
    }

    [RelayCommand]
    private async Task BrowseClonePathAsync()
    {
        var path = await PickFolderAsync();
        if (path != null) CloneLocalPath = path;
    }

    private async Task<string?> PickFolderAsync()
    {
        if (_window == null) return null;

        var picker = new FolderPicker();
        picker.SuggestedStartLocation = PickerLocationId.Desktop;
        picker.FileTypeFilter.Add("*");

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(_window);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        var folder = await picker.PickSingleFolderAsync();
        return folder?.Path;
    }

    // ─── 新規作成実行 ──────────────────

    [RelayCommand]
    private async Task InitRepositoryAsync()
    {
        if (_xamlRoot == null) return;

        if (string.IsNullOrWhiteSpace(InitLocalPath))
        {
            await DialogHelper.ShowErrorAsync(_xamlRoot, "入力エラー",
                "リポジトリを作成するフォルダを指定してください。");
            return;
        }

        IsBusy = true;
        StatusMessage = "リポジトリを初期化しています...";
        try
        {
            var remoteUrl = string.IsNullOrWhiteSpace(InitRemoteUrl) ? null : InitRemoteUrl.Trim();
            await _git.InitRepositoryAsync(InitLocalPath.Trim(), remoteUrl, AutoInitBare);

            var msg = "リポジトリを初期化しました。\nREADME.md を生成し、Initial commit を作成しました。\nブランチ: main";
            if (remoteUrl != null)
                msg += $"\nリモート 'origin' を設定しました：\n{remoteUrl}";

            StatusMessage = "✓ " + msg;
            await DialogHelper.ShowInfoAsync(_xamlRoot, "リポジトリ作成完了", msg);
            SetupCompleted?.Invoke();
        }
        catch (Exception ex)
        {
            StatusMessage = "初期化に失敗しました。";
            await DialogHelper.ShowErrorAsync(_xamlRoot, "初期化エラー", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ─── クローン実行 ──────────────────

    [RelayCommand]
    private async Task CloneRepositoryAsync()
    {
        if (_xamlRoot == null) return;

        if (string.IsNullOrWhiteSpace(CloneRemoteUrl))
        {
            await DialogHelper.ShowErrorAsync(_xamlRoot, "入力エラー",
                "クローン元のリモートURL（またはパス）を入力してください。");
            return;
        }
        if (string.IsNullOrWhiteSpace(CloneLocalPath))
        {
            await DialogHelper.ShowErrorAsync(_xamlRoot, "入力エラー",
                "クローン先のローカルフォルダを指定してください。");
            return;
        }

        IsBusy = true;
        StatusMessage = "クローン中...（リポジトリのサイズによって時間がかかることがあります）";
        try
        {
            await _git.CloneRepositoryAsync(CloneRemoteUrl.Trim(), CloneLocalPath.Trim());

            StatusMessage = "✓ クローンが完了しました。";
            await DialogHelper.ShowInfoAsync(_xamlRoot, "クローン完了",
                $"リポジトリをクローンしました。\n{CloneLocalPath}");
            SetupCompleted?.Invoke();
        }
        catch (Exception ex)
        {
            StatusMessage = "クローンに失敗しました。";
            await DialogHelper.ShowErrorAsync(_xamlRoot, "クローンエラー", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
