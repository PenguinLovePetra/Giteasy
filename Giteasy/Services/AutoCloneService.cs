using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Giteasy.Models;

namespace Giteasy.Services;

/// <summary>
/// 監視ディレクトリ内に新しい Git bare リポジトリが作成されたとき、
/// 自動的にクローンしてプロジェクト一覧に登録するサービスです。
/// </summary>
public class AutoCloneService : IDisposable
{
    private readonly GitService _git;
    private readonly DatabaseService _db;
    private FileSystemWatcher? _watcher;
    private readonly HashSet<string> _processing = new();
    private readonly object _lock = new();

    /// <summary>クローン完了時に発火。UI スレッドで受け取る場合は DispatcherQueue 経由で。</summary>
    public event Action<ProjectInfo>? ProjectAutoCloned;

    /// <summary>エラー発生時に発火。</summary>
    public event Action<string>? ErrorOccurred;

    /// <summary>現在監視中かどうか。</summary>
    public bool IsWatching => _watcher?.EnableRaisingEvents == true;

    public AutoCloneService(GitService git, DatabaseService db)
    {
        _git = git;
        _db = db;
    }

    /// <summary>
    /// 指定したディレクトリの監視を開始します。
    /// 新規サブディレクトリが bare リポジトリであればクローンを実行します。
    /// </summary>
    /// <param name="watchDir">監視対象ディレクトリ（bare リポジトリが作成される親ディレクトリ）</param>
    /// <param name="cloneBaseDir">クローン先のベースディレクトリ</param>
    public void StartWatching(string watchDir, string cloneBaseDir)
    {
        StopWatching();

        if (!Directory.Exists(watchDir))
            Directory.CreateDirectory(watchDir);
        if (!Directory.Exists(cloneBaseDir))
            Directory.CreateDirectory(cloneBaseDir);

        _watcher = new FileSystemWatcher(watchDir)
        {
            NotifyFilter = NotifyFilters.DirectoryName,
            IncludeSubdirectories = false,
            EnableRaisingEvents = true,
        };

        _watcher.Created += async (_, e) =>
        {
            // ディレクトリのみ処理
            if (!Directory.Exists(e.FullPath)) return;

            // bare リポ判定に遅延を入れる（init 完了を待つ）
            await Task.Delay(2000);

            await TryAutoCloneAsync(e.FullPath, cloneBaseDir);
        };

        GitLogService.Log($"[AutoClone] 監視開始: {watchDir}");
    }

    /// <summary>監視を停止します。</summary>
    public void StopWatching()
    {
        if (_watcher != null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
            _watcher = null;
            GitLogService.Log("[AutoClone] 監視停止");
        }
    }

    /// <summary>
    /// 指定パスが bare リポジトリかを判定し、クローンを実行します。
    /// </summary>
    private async Task TryAutoCloneAsync(string repoPath, string cloneBaseDir)
    {
        // 二重処理防止
        lock (_lock)
        {
            if (_processing.Contains(repoPath)) return;
            _processing.Add(repoPath);
        }

        try
        {
            // bare リポジトリかどうかの判定（HEAD ファイルの存在）
            if (!IsBareRepository(repoPath))
            {
                GitLogService.Log($"[AutoClone] bare リポジトリではないためスキップ: {repoPath}");
                return;
            }

            var repoName = Path.GetFileName(repoPath);
            // .git 拡張子の除去（bare リポは *.git という命名が多い）
            if (repoName.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
                repoName = repoName[..^4];
            if (string.IsNullOrEmpty(repoName))
                repoName = "unnamed-repo";

            var clonePath = Path.Combine(cloneBaseDir, repoName);

            // 既にクローン済みならスキップ
            if (Directory.Exists(clonePath) && Directory.GetFileSystemEntries(clonePath).Length > 0)
            {
                GitLogService.Log($"[AutoClone] 既にクローン済み: {clonePath}");
                return;
            }

            GitLogService.Log($"[AutoClone] クローン開始: {repoPath} → {clonePath}");

            await _git.CloneRepositoryAsync(repoPath, clonePath);

            // プロジェクト登録
            var project = new ProjectInfo
            {
                Name = repoName,
                LocalPath = clonePath,
                RemoteUrl = repoPath,
            };
            _db.AddProject(project);

            GitLogService.Log($"[AutoClone] クローン完了 & プロジェクト登録: {repoName}");
            ProjectAutoCloned?.Invoke(project);
        }
        catch (Exception ex)
        {
            var msg = $"[AutoClone] クローン失敗: {repoPath} — {ex.Message}";
            GitLogService.Log(msg);
            ErrorOccurred?.Invoke(msg);
        }
        finally
        {
            lock (_lock)
            {
                _processing.Remove(repoPath);
            }
        }
    }

    /// <summary>
    /// 指定パスが Git bare リポジトリかどうかを判定します。
    /// HEAD ファイルと objects/ refs/ ディレクトリが存在することで判定します。
    /// </summary>
    private static bool IsBareRepository(string path)
    {
        if (!Directory.Exists(path)) return false;

        var headFile = Path.Combine(path, "HEAD");
        var objectsDir = Path.Combine(path, "objects");
        var refsDir = Path.Combine(path, "refs");

        return File.Exists(headFile)
            && Directory.Exists(objectsDir)
            && Directory.Exists(refsDir);
    }

    public void Dispose()
    {
        StopWatching();
        GC.SuppressFinalize(this);
    }
}
