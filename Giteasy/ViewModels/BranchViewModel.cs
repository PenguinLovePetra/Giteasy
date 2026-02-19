using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Giteasy.Helpers;
using Giteasy.Models;
using Giteasy.Services;
using LibGit2Sharp;
using Microsoft.UI.Xaml;

namespace Giteasy.ViewModels;

public partial class BranchViewModel : ObservableObject
{
    private readonly GitService _git;
    private XamlRoot? _xamlRoot;

    [ObservableProperty]
    private string _newBranchName = "";

    [ObservableProperty]
    private BranchInfo? _selectedBranch;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _currentBranchName = "";

    public ObservableCollection<BranchInfo> Branches { get; } = new();

    public BranchViewModel(GitService git)
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
            var branches = await Task.Run(() => _git.GetBranches());
            Branches.Clear();
            foreach (var b in branches)
                Branches.Add(b);
            CurrentBranchName = _git.CurrentBranchName;
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
    private async Task CreateBranchAsync()
    {
        if (_xamlRoot == null) return;

        if (string.IsNullOrWhiteSpace(NewBranchName))
        {
            await DialogHelper.ShowErrorAsync(_xamlRoot, "入力エラー", "ブランチ名を入力してください。");
            return;
        }

        IsBusy = true;
        try
        {
            await Task.Run(() => _git.CreateBranch(NewBranchName));
            await DialogHelper.ShowInfoAsync(_xamlRoot, "作成完了", $"ブランチ '{NewBranchName}' を作成しました。");
            NewBranchName = "";
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            await DialogHelper.ShowErrorAsync(_xamlRoot, "ブランチ作成エラー", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CheckoutAsync()
    {
        if (_xamlRoot == null || SelectedBranch == null) return;
        if (SelectedBranch.IsHead) return;

        IsBusy = true;
        try
        {
            var name = SelectedBranch.Name;
            await Task.Run(() => _git.Checkout(name));
            await DialogHelper.ShowInfoAsync(_xamlRoot, "切替完了", $"ブランチ '{name}' に切り替えました。");
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            await DialogHelper.ShowErrorAsync(_xamlRoot, "ブランチ切替エラー", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task MergeAsync()
    {
        if (_xamlRoot == null || SelectedBranch == null) return;
        if (SelectedBranch.IsHead)
        {
            await DialogHelper.ShowErrorAsync(_xamlRoot, "マージエラー", "現在のブランチ自身をマージすることはできません。");
            return;
        }

        var confirmed = await DialogHelper.ShowConfirmAsync(_xamlRoot,
            "ブランチのマージ",
            $"'{SelectedBranch.Name}' を現在のブランチにマージしますか？",
            "マージする", "キャンセル");
        if (!confirmed) return;

        IsBusy = true;
        try
        {
            var branchName = SelectedBranch.Name;
            var result = await Task.Run(() => _git.Merge(branchName));
            if (result.Status == MergeStatus.Conflicts)
            {
                await DialogHelper.ShowErrorAsync(_xamlRoot, "競合が発生しました",
                    "マージ中に競合が発生しました。\nステータス画面で競合ファイルを確認してください。");
            }
            else
            {
                await DialogHelper.ShowInfoAsync(_xamlRoot, "マージ完了", $"'{branchName}' をマージしました。");
            }
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            await DialogHelper.ShowErrorAsync(_xamlRoot, "マージエラー", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DeleteBranchAsync()
    {
        if (_xamlRoot == null || SelectedBranch == null) return;
        if (SelectedBranch.IsHead)
        {
            await DialogHelper.ShowErrorAsync(_xamlRoot, "削除エラー", "現在のブランチは削除できません。\n別のブランチに切り替えてから削除してください。");
            return;
        }
        if (SelectedBranch.IsRemote)
        {
            await DialogHelper.ShowErrorAsync(_xamlRoot, "削除エラー", "リモートブランチはこの画面から削除できません。");
            return;
        }

        var confirmed = await DialogHelper.ShowConfirmAsync(_xamlRoot,
            "ブランチの削除",
            $"ブランチ '{SelectedBranch.Name}' を削除しますか？\nこの操作は元に戻せません。",
            "削除する", "キャンセル");
        if (!confirmed) return;

        IsBusy = true;
        try
        {
            var name = SelectedBranch.Name;
            await Task.Run(() => _git.DeleteBranch(name));
            await DialogHelper.ShowInfoAsync(_xamlRoot, "削除完了", $"ブランチ '{name}' を削除しました。");
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            await DialogHelper.ShowErrorAsync(_xamlRoot, "ブランチ削除エラー", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
