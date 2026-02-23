using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Giteasy.Helpers;
using Giteasy.Models;
using Giteasy.Services;
using Microsoft.UI.Xaml;

namespace Giteasy.ViewModels;

public partial class HistoryViewModel : ObservableObject
{
    private readonly GitService _git;
    private XamlRoot? _xamlRoot;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private CommitInfo? _selectedCommit;

    public ObservableCollection<CommitInfo> Commits { get; } = new();
    public ObservableCollection<GraphNode> GraphNodes { get; } = new();

    public HistoryViewModel(GitService git)
    {
        _git = git;
    }

    public void SetXamlRoot(XamlRoot root) => _xamlRoot = root;

    [RelayCommand]
    public async Task RefreshAsync()
    {
        if (!_git.IsRepositorySet) return;

        IsBusy = true;
        try
        {
            var commits = await Task.Run(() => _git.GetCommitLog());
            var nodes = await Task.Run(() => GraphService.BuildGraph(commits));

            Commits.Clear();
            GraphNodes.Clear();
            foreach (var c in commits)
                Commits.Add(c);
            foreach (var n in nodes)
                GraphNodes.Add(n);
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
    private async Task RevertAsync()
    {
        if (_xamlRoot == null || SelectedCommit == null) return;

        var confirmed = await DialogHelper.ShowConfirmAsync(_xamlRoot,
            "コミットの取り消し（Revert）",
            $"以下のコミットを取り消しますか？\n\n" +
            $"  {SelectedCommit.ShortSha} : {SelectedCommit.Message}\n\n" +
            "この操作は「取り消しコミット」を新たに作成します。\n履歴は消えないので安全です。",
            "取り消す", "キャンセル");

        if (!confirmed) return;

        IsBusy = true;
        try
        {
            var sha = SelectedCommit.FullSha;
            var result = await Task.Run(() => _git.RevertCommit(sha));
            if (result == "Conflicts")
            {
                await DialogHelper.ShowErrorAsync(_xamlRoot, "競合が発生しました",
                    "Revert 中に競合が発生しました。\nステータス画面で競合ファイルを確認してください。");
            }
            else
            {
                await DialogHelper.ShowInfoAsync(_xamlRoot, "Revert 完了",
                    "コミットを取り消しました。新しい取り消しコミットが作成されました。");
            }
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            await DialogHelper.ShowErrorAsync(_xamlRoot, "Revert エラー", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
