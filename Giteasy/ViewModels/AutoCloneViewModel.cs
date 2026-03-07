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
    private DispatcherQueue? _dispatcherQueue;

    // ─── バインディングプロパティ ───────────

    [ObservableProperty]
    private string _watchDirectory = "";

    [ObservableProperty]
    private string _cloneBaseDirectory = "";

    [ObservableProperty]
    private bool _isChecking;

    [ObservableProperty]
    private string _statusMessage = "設定を保存し、「今すぐチェック」でスキャンを実行できます。";

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
    public void SetDispatcherQueue(DispatcherQueue queue) => _dispatcherQueue = queue;

    // ─── コマンド ──────────────────────────

    /// <summary>設定をDBに保存します。</summary>
    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        if (_xamlRoot == null) return;

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

        _db.SetSetting("autoclone_watch_dir", WatchDirectory.Trim());
        _db.SetSetting("autoclone_clone_dir", CloneBaseDirectory.Trim());

        StatusMessage = "✓ 設定を保存しました。";
        await DialogHelper.ShowInfoAsync(_xamlRoot, "保存完了",
            "自動クローンの設定を保存しました。\nアプリ起動時に自動でチェックされます。");
    }

    /// <summary>今すぐスキャンを実行します。</summary>
    [RelayCommand]
    private async Task CheckNowAsync()
    {
        if (_xamlRoot == null) return;

        if (string.IsNullOrWhiteSpace(WatchDirectory) || string.IsNullOrWhiteSpace(CloneBaseDirectory))
        {
            await DialogHelper.ShowErrorAsync(_xamlRoot, "設定が未完了",
                "監視ディレクトリとクローン先ディレクトリを設定してください。");
            return;
        }

        // 設定を保存（最新の値を反映）
        _db.SetSetting("autoclone_watch_dir", WatchDirectory.Trim());
        _db.SetSetting("autoclone_clone_dir", CloneBaseDirectory.Trim());

        await RunScanAsync();
    }

    /// <summary>
    /// アプリ起動時に呼ばれるスタートアップチェック。
    /// DB に設定が保存されていればスキャンを実行します。
    /// </summary>
    public async Task RunStartupCheckAsync()
    {
        if (string.IsNullOrWhiteSpace(WatchDirectory) || string.IsNullOrWhiteSpace(CloneBaseDirectory))
            return;

        await RunScanAsync();
    }

    /// <summary>スキャンを実行し、結果をUIに反映します。</summary>
    private async Task RunScanAsync()
    {
        IsChecking = true;
        StatusMessage = "スキャン中...";

        try
        {
            var result = await _autoCloneService.ScanAndCloneAsync(
                WatchDirectory.Trim(), CloneBaseDirectory.Trim());

            if (result.Cloned > 0)
            {
                StatusMessage = $"✓ {result.Cloned} 件のリポジトリをクローンしました。（スキップ: {result.Skipped} 件）";
            }
            else if (result.Total == 0)
            {
                StatusMessage = "スキャン完了: 監視ディレクトリにリポジトリが見つかりませんでした。";
            }
            else
            {
                StatusMessage = $"スキャン完了: 新しいリポジトリはありません。（スキップ: {result.Skipped} 件）";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"スキャンエラー: {ex.Message}";
            GitLogService.Log($"[AutoClone] スキャンエラー: {ex.Message}");
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
