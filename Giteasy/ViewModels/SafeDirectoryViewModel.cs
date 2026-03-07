using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Giteasy.Helpers;
using Giteasy.Services;
using Microsoft.UI.Xaml;

namespace Giteasy.ViewModels;

public partial class SafeDirectoryViewModel : ObservableObject
{
    private readonly SafeDirectoryService _safeDir;
    private XamlRoot? _xamlRoot;

    [ObservableProperty]
    private string _newDirectory = "";

    [ObservableProperty]
    private string? _selectedDirectory;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = "";

    public ObservableCollection<string> Directories { get; } = new();

    public SafeDirectoryViewModel(SafeDirectoryService safeDir)
    {
        _safeDir = safeDir;
    }

    public void SetXamlRoot(XamlRoot root) => _xamlRoot = root;

    [RelayCommand]
    public async Task RefreshAsync()
    {
        IsBusy = true;
        try
        {
            var dirs = await Task.Run(() => _safeDir.GetSafeDirectories());
            Directories.Clear();
            foreach (var d in dirs)
                Directories.Add(d);
            StatusMessage = $"{dirs.Count} 件の safe directory が登録されています。";
        }
        catch (Exception ex)
        {
            StatusMessage = $"取得エラー: {ex.Message}";
            GitLogService.Log($"[SafeDirectory] 一覧取得エラー: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task AddDirectoryAsync()
    {
        if (_xamlRoot == null) return;

        if (string.IsNullOrWhiteSpace(NewDirectory))
        {
            await DialogHelper.ShowErrorAsync(_xamlRoot, "入力エラー",
                "ディレクトリパスを入力してください。");
            return;
        }

        IsBusy = true;
        try
        {
            await Task.Run(() => _safeDir.AddSafeDirectory(NewDirectory.Trim()));
            NewDirectory = "";
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            await DialogHelper.ShowErrorAsync(_xamlRoot, "追加エラー", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RemoveDirectoryAsync()
    {
        if (_xamlRoot == null || SelectedDirectory == null) return;

        var confirmed = await DialogHelper.ShowConfirmAsync(_xamlRoot,
            "safe directory の削除",
            $"以下のパスを safe directory から削除しますか？\n\n{SelectedDirectory}",
            "削除する", "キャンセル");
        if (!confirmed) return;

        IsBusy = true;
        try
        {
            var dir = SelectedDirectory;
            await Task.Run(() => _safeDir.RemoveSafeDirectory(dir));
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            await DialogHelper.ShowErrorAsync(_xamlRoot, "削除エラー", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
