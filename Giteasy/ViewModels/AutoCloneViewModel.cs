using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Giteasy.Helpers;
using Giteasy.Models;
using Giteasy.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace Giteasy.ViewModels;

public partial class AutoCloneViewModel : ObservableObject
{
    private readonly AutoCloneService _autoCloneService;
    private readonly DatabaseService _db;
    private XamlRoot? _xamlRoot;
    private Window? _window;
    private DispatcherQueue? _dispatcherQueue;

    // ─── バインディングプロパティ ───────────

    private string _watchDirectory = "";
    public string WatchDirectory
    {
        get => _watchDirectory;
        set
        {
            if (SetProperty(ref _watchDirectory, value))
            {
                _db.SetSetting("autoclone_watch_dir", value.Trim());
            }
        }
    }

    private string _cloneBaseDirectory = "";
    public string CloneBaseDirectory
    {
        get => _cloneBaseDirectory;
        set
        {
            if (SetProperty(ref _cloneBaseDirectory, value))
            {
                _db.SetSetting("autoclone_clone_dir", value.Trim());
            }
        }
    }

    [ObservableProperty]
    private string _statusMessage = "準備完了";

    [ObservableProperty]
    private bool _isChecking;

    /// <summary>クローン履歴（UIバインディング用）。</summary>
    public ObservableCollection<AutoCloneLogEntry> CloneHistory { get; } = new();

    /// <summary>プロジェクト一覧のリフレッシュを要求するイベント。</summary>
    public event Action? ProjectListChanged;

    public AutoCloneViewModel(AutoCloneService autoCloneService, DatabaseService db)
    {
        _autoCloneService = autoCloneService;
        _db = db;

        // DB から前回の設定を復元
        _watchDirectory = _db.GetSetting("autoclone_watch_dir") ?? "";
        _cloneBaseDirectory = _db.GetSetting("autoclone_clone_dir") ?? "";

        // イベント購読
        _autoCloneService.ProjectAutoCloned += OnProjectAutoCloned;
        _autoCloneService.ErrorOccurred += OnErrorOccurred;
    }

    public void SetXamlRoot(XamlRoot root) => _xamlRoot = root;
    public void SetWindow(Window window) => _window = window;
    public void SetDispatcherQueue(DispatcherQueue queue) => _dispatcherQueue = queue;

    // ─── コマンド ──────────────────────────

    [RelayCommand]
    private async Task CheckNowAsync()
    {
        if (_xamlRoot == null || IsChecking) return;

        // バリデーション
        if (string.IsNullOrWhiteSpace(WatchDirectory))
        {
            await DialogHelper.ShowErrorAsync(_xamlRoot, "入力エラー",
                "監視ディレクトリを指定してください。");
            return;
        }
        if (string.IsNullOrWhiteSpace(CloneBaseDirectory))
        {
            await DialogHelper.ShowErrorAsync(_xamlRoot, "入力エラー",
                "クローン先ディレクトリを指定してください。");
            return;
        }

        IsChecking = true;
        StatusMessage = "確認中...";

        try
        {
            await _autoCloneService.RunAutoCloneOnceAsync(WatchDirectory.Trim(), CloneBaseDirectory.Trim());
            StatusMessage = $"確認完了 ({DateTime.Now:HH:mm:ss})";
        }
        catch (Exception ex)
        {
            StatusMessage = "エラーが発生しました";
            await DialogHelper.ShowErrorAsync(_xamlRoot, "確認エラー", ex.Message);
        }
        finally
        {
            IsChecking = false;
        }
    }

    // ─── イベントハンドラ ──────────────────

    private void OnProjectAutoCloned(ProjectInfo project)
    {
        _dispatcherQueue?.TryEnqueue(() =>
        {
            CloneHistory.Insert(0, new AutoCloneLogEntry
            {
                Timestamp = DateTime.Now,
                RepoName = project.Name,
                ClonePath = project.LocalPath,
                Status = "✓ 成功",
            });
            StatusMessage = $"クローン成功: {project.Name} ({DateTime.Now:HH:mm:ss})";
            ProjectListChanged?.Invoke();
        });
    }

    private void OnErrorOccurred(string message)
    {
        _dispatcherQueue?.TryEnqueue(() =>
        {
            CloneHistory.Insert(0, new AutoCloneLogEntry
            {
                Timestamp = DateTime.Now,
                RepoName = "(エラー)",
                ClonePath = "",
                Status = message,
            });
        });
    }
}

/// <summary>クローン履歴の1行分データ。</summary>
public class AutoCloneLogEntry
{
    public DateTime Timestamp { get; set; }
    public string RepoName { get; set; } = "";
    public string ClonePath { get; set; } = "";
    public string Status { get; set; } = "";
    public string TimestampText => Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
}
