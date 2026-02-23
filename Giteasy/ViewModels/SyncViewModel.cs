using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Giteasy.Helpers;
using Giteasy.Services;
using Microsoft.UI.Xaml;

namespace Giteasy.ViewModels;

public partial class SyncViewModel : ObservableObject
{
    private readonly GitService _git;
    private XamlRoot? _xamlRoot;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = "";

    [ObservableProperty]
    private string _currentBranchName = "";

    public SyncViewModel(GitService git)
    {
        _git = git;
    }

    public void SetXamlRoot(XamlRoot root) => _xamlRoot = root;

    public void Refresh()
    {
        if (_git.IsRepositorySet)
        {
            CurrentBranchName = _git.CurrentBranchName;
            StatusMessage = "同期の準備ができました。";
        }
        else
        {
            CurrentBranchName = "未設定";
            StatusMessage = "リポジトリが設定されていません。";
        }
    }

    [RelayCommand]
    private async Task FetchAsync()
    {
        if (_xamlRoot == null) return;

        IsBusy = true;
        StatusMessage = "フェッチ中...";
        try
        {
            await _git.FetchAsync();
            StatusMessage = "✓ フェッチが完了しました。";
            await DialogHelper.ShowInfoAsync(_xamlRoot, "完了", "リモートの最新情報を取得しました。");
        }
        catch (Exception ex)
        {
            StatusMessage = "フェッチに失敗しました。";
            GitLogService.Log($"[フェッチエラー] {ex.Message}");
            await DialogHelper.ShowExceptionAsync(_xamlRoot, "フェッチエラー", ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task PullAsync()
    {
        if (_xamlRoot == null) return;

        IsBusy = true;
        StatusMessage = "Pull 実行中...";
        try
        {
            var result = await _git.PullAsync();
            if (result == "Conflicts")
            {
                StatusMessage = "⚠ 競合が発生しました。";
                await DialogHelper.ShowErrorAsync(_xamlRoot, "競合が発生しました",
                    "Pull 中に競合が発生しました。\nステータス画面で競合ファイルを確認し、手動で解決してください。");
            }
            else if (result == "UpToDate")
            {
                StatusMessage = "✓ すでに最新です。";
                await DialogHelper.ShowInfoAsync(_xamlRoot, "最新です", "ローカルはリモートと同じ最新の状態です。");
            }
            else
            {
                StatusMessage = "✓ Pull が完了しました。";
                await DialogHelper.ShowInfoAsync(_xamlRoot, "Pull 完了", "リモートの変更をローカルに取り込みました。");
            }
            CurrentBranchName = _git.CurrentBranchName;
        }
        catch (Exception ex)
        {
            StatusMessage = "Pull に失敗しました。";
            GitLogService.Log($"[Pullエラー] {ex.Message}");
            await DialogHelper.ShowExceptionAsync(_xamlRoot, "Pull エラー", ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task PushAsync()
    {
        if (_xamlRoot == null) return;

        IsBusy = true;
        StatusMessage = "Push 実行中...";
        try
        {
            await _git.PushAsync();
            StatusMessage = "✓ Push が完了しました。";
            await DialogHelper.ShowInfoAsync(_xamlRoot, "Push 完了",
                "ローカルの変更をリモートに送信しました。");
        }
        catch (Exception ex)
        {
            StatusMessage = "Push に失敗しました。";
            GitLogService.Log($"[Pushエラー] {ex.Message}");
            await DialogHelper.ShowExceptionAsync(_xamlRoot, "Push エラー", ex);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
