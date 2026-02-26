using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Giteasy.Models;

namespace Giteasy.Services;

/// <summary>
/// 指定ディレクトリ内の Git bare リポジトリを走査し、
/// 未クローンのものを一括でクローンしてプロジェクト一覧に登録するサービスです。
/// </summary>
public class AutoCloneService
{
    private readonly GitService _git;
    private readonly DatabaseService _db;
    private readonly HashSet<string> _processing = new();
    private readonly object _lock = new();

    /// <summary>クローン完了時に発火。UI スレッドで受け取る場合は DispatcherQueue 経由で。</summary>
    public event Action<ProjectInfo>? ProjectAutoCloned;

    /// <summary>エラー発生時に発火。</summary>
    public event Action<string>? ErrorOccurred;

    public AutoCloneService(GitService git, DatabaseService db)
    {
        _git = git;
        _db = db;
    }

    /// <summary>
    /// 指定したディレクトリ内のすべてのサブディレクトリをチェックし、
    /// bare リポジトリであればクローンを実行します（1回のみ実行）。
    /// </summary>
    /// <param name="watchDir">監視対象ディレクトリ（bare リポジトリが作成される親ディレクトリ）</param>
    /// <param name="cloneBaseDir">クローン先のベースディレクトリ</param>
    public async Task RunAutoCloneOnceAsync(string watchDir, string cloneBaseDir)
    {
        if (string.IsNullOrWhiteSpace(watchDir) || string.IsNullOrWhiteSpace(cloneBaseDir))
            return;

        if (!Directory.Exists(watchDir) || !Directory.Exists(cloneBaseDir))
            return;

        GitLogService.Log($"[AutoClone] 起動時/一括チェック開始: {watchDir}");

        try
        {
            var directories = Directory.GetDirectories(watchDir);
            foreach (var dir in directories)
            {
                await TryAutoCloneAsync(dir, cloneBaseDir);
            }
        }
        catch (Exception ex)
        {
            GitLogService.Log($"[AutoClone] チェック中にエラー発生: {ex.Message}");
        }
        
        GitLogService.Log("[AutoClone] 起動時/一括チェック完了");
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
                return; // ログ抑制のため静かにスキップ
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
                // 静かにスキップ
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
}
