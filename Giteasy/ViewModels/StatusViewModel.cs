using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Giteasy.Helpers;
using Giteasy.Models;
using Giteasy.Services;
using Microsoft.UI.Xaml;

namespace Giteasy.ViewModels;

public partial class StatusViewModel : ObservableObject
{
    private readonly GitService _git;
    private XamlRoot? _xamlRoot;

    [ObservableProperty]
    private string _commitMessage = "";

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = "";

    public ObservableCollection<FileChange> ChangedFiles { get; } = new();

    public StatusViewModel(GitService git)
    {
        _git = git;
    }

    public void SetXamlRoot(XamlRoot root) => _xamlRoot = root;

    [RelayCommand]
    public async Task RefreshAsync()
    {
        if (!_git.IsRepositorySet)
        {
            StatusMessage = "リポジトリが設定されていません。設定画面からパスを指定してください。";
            return;
        }

        IsBusy = true;
        try
        {
            var files = await Task.Run(() => _git.GetChangedFiles());
            ChangedFiles.Clear();
            foreach (var f in files)
                ChangedFiles.Add(f);
            StatusMessage = files.Count == 0 ? "✓ 変更はありません" : $"{files.Count} 件の変更があります";
        }
        catch (Exception ex)
        {
            if (_xamlRoot != null)
                await DialogHelper.ShowErrorAsync(_xamlRoot, "エラー", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var f in ChangedFiles)
            f.IsSelected = true;
    }

    [RelayCommand]
    private void DeselectAll()
    {
        foreach (var f in ChangedFiles)
            f.IsSelected = false;
    }

    [RelayCommand]
    private async Task CommitAsync()
    {
        if (_xamlRoot == null) return;

        if (string.IsNullOrWhiteSpace(CommitMessage))
        {
            await DialogHelper.ShowErrorAsync(_xamlRoot, "コミットメッセージが必要です",
                "コミットメッセージを入力してください。\n何を変更したのか、簡単に書きましょう。");
            return;
        }

        var selectedFiles = ChangedFiles.Where(f => f.IsSelected).ToList();
        if (selectedFiles.Count == 0)
        {
            await DialogHelper.ShowErrorAsync(_xamlRoot, "ファイルが選択されていません",
                "コミットするファイルを選択してください。\n「全選択」ボタンで全ファイルを選択できます。");
            return;
        }

        IsBusy = true;
        try
        {
            await Task.Run(() =>
            {
                _git.StageFiles(selectedFiles.Select(f => f.FilePath));
                _git.Commit(CommitMessage);
            });
            CommitMessage = "";
            await DialogHelper.ShowInfoAsync(_xamlRoot, "コミット完了",
                $"{selectedFiles.Count} 件のファイルをコミットしました。");
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            await DialogHelper.ShowErrorAsync(_xamlRoot, "コミットエラー", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DiscardChangesAsync()
    {
        if (_xamlRoot == null) return;

        var selectedFiles = ChangedFiles.Where(f => f.IsSelected).ToList();
        if (selectedFiles.Count == 0)
        {
            await DialogHelper.ShowErrorAsync(_xamlRoot, "ファイルが選択されていません",
                "変更を取り消すファイルを選択してください。");
            return;
        }

        var confirmed = await DialogHelper.ShowConfirmAsync(_xamlRoot,
            "編集をすべて取り消して元に戻す",
            $"{selectedFiles.Count} 件のファイルの変更を取り消しますか？\nこの操作は元に戻せません。",
            "取り消す", "キャンセル");

        if (!confirmed) return;

        IsBusy = true;
        try
        {
            await Task.Run(() => _git.DiscardChanges(selectedFiles.Select(f => f.FilePath)));
            await DialogHelper.ShowInfoAsync(_xamlRoot, "完了", "変更を取り消しました。");
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            await DialogHelper.ShowErrorAsync(_xamlRoot, "エラー", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
