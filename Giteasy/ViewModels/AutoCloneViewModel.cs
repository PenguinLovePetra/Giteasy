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

    [ObservableProperty]
    private string _watchDirectory = "";

    [ObservableProperty]
    private string _cloneBaseDirectory = "";

    [ObservableProperty]
    private bool _isWatching;

    [ObservableProperty]
    private string _statusMessage = "監視は停止中です。";

    /// <summary>クローン履歴（UIバインディング用）。</summary>
    public ObservableCollection<AutoCloneLogEntry> CloneHistory { get; } = new();

    /// <summary>プロジェクト一覧のリフレッシュを要求するイベント。</summary>
    public event Action? ProjectListChanged;

    public AutoCloneViewModel(AutoCloneService autoCloneService, DatabaseService db)
    {
        _autoCloneService = autoCloneService;
        _db = db;

        // DB から前回の設定を復元
        WatchDirectory = _db.GetSetting("autoclone_watch_dir") ?? "";
        CloneBaseDirectory = _db.GetSetting("autoclone_clone_dir") ?? "";

        // イベント購読
        _autoCloneService.ProjectAutoCloned += OnProjectAutoCloned;
        _autoCloneService.ErrorOccurred += OnErrorOccurred;
    }

    public void SetXamlRoot(XamlRoot root) => _xamlRoot = root;
    public void SetWindow(Window window) => _window = window;
    public void SetDispatcherQueue(DispatcherQueue queue) => _dispatcherQueue = queue;

    // ─── コマンド ──────────────────────────

    [RelayCommand]
    private async Task ToggleWatchingAsync()
    {
        if (_xamlRoot == null) return;

        if (IsWatching)
        {
            _autoCloneService.StopWatching();
            IsWatching = false;
            StatusMessage = "監視を停止しました。";
            return;
        }

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

        try
        {
            // 設定を保存
            _db.SetSetting("autoclone_watch_dir", WatchDirectory.Trim());
            _db.SetSetting("autoclone_clone_dir", CloneBaseDirectory.Trim());

            _autoCloneService.StartWatching(WatchDirectory.Trim(), CloneBaseDirectory.Trim());
            IsWatching = true;
            StatusMessage = $"監視中: {WatchDirectory}";
        }
        catch (Exception ex)
        {
            await DialogHelper.ShowErrorAsync(_xamlRoot, "監視開始エラー", ex.Message);
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
            StatusMessage = $"最後のクローン: {project.Name} ({DateTime.Now:HH:mm:ss})";
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
