using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Giteasy.Models;

namespace Giteasy.Services;

/// <summary>
/// 指定ディレクトリ内の Git bare リポジトリを検出し、
/// 未クローンのものを自動的にクローンしてプロジェクト一覧に登録するサービスです。
/// アプリ起動時またはユーザー操作時にワンショットでスキャンを実行します。
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

    /// <summary>スキップ時に発火（既にクローン済み、bare でない等）。</summary>
    public event Action<string>? ItemSkipped;

    public AutoCloneService(GitService git, DatabaseService db)
    {
        _git = git;
        _db = db;
    }

    /// <summary>
    /// 監視ディレクトリ内のサブディレクトリをスキャンし、
    /// bare リポジトリかつ未クローンのものをクローンします。
    /// </summary>
    /// <param name="watchDir">スキャン対象ディレクトリ（bare リポジトリが格納される親ディレクトリ）</param>
    /// <param name="cloneBaseDir">クローン先のベースディレクトリ</param>
    /// <returns>スキャン結果（クローン成功数、スキップ数、エラー数）</returns>
    public async Task<ScanResult> ScanAndCloneAsync(string watchDir, string cloneBaseDir)
    {
        var result = new ScanResult();

        if (!Directory.Exists(watchDir))
        {
            GitLogService.Log($"[AutoClone] 監視ディレクトリが存在しません: {watchDir}");
            return result;
        }

        if (!Directory.Exists(cloneBaseDir))
            Directory.CreateDirectory(cloneBaseDir);

        GitLogService.Log($"[AutoClone] スキャン開始: {watchDir}");

        string[] subdirs;
        try
        {
            subdirs = Directory.GetDirectories(watchDir);
        }
        catch (Exception ex)
        {
            GitLogService.Log($"[AutoClone] ディレクトリ列挙エラー: {ex.Message}");
            ErrorOccurred?.Invoke($"ディレクトリの読み取りに失敗しました: {ex.Message}");
            return result;
        }

        foreach (var subdir in subdirs)
        {
            if (!IsBareRepository(subdir))
            {
                result.Skipped++;
                continue;
            }

            var cloneResult = await TryAutoCloneAsync(subdir, cloneBaseDir);
            switch (cloneResult)
            {
                case CloneResult.Cloned:
                    result.Cloned++;
                    break;
                case CloneResult.AlreadyExists:
                    result.Skipped++;
                    break;
                case CloneResult.Error:
                    result.Errors++;
                    break;
            }
        }

        GitLogService.Log($"[AutoClone] スキャン完了: クローン {result.Cloned} 件, スキップ {result.Skipped} 件, エラー {result.Errors} 件");
        return result;
    }

    /// <summary>
    /// 指定パスが bare リポジトリかを判定し、クローンを実行します。
    /// </summary>
    public async Task<CloneResult> TryAutoCloneAsync(string repoPath, string cloneBaseDir)
    {
        // 二重処理防止
        lock (_lock)
        {
            if (_processing.Contains(repoPath)) return CloneResult.AlreadyExists;
            _processing.Add(repoPath);
        }

        try
        {
            if (!IsBareRepository(repoPath))
            {
                ItemSkipped?.Invoke($"bare リポジトリではないためスキップ: {Path.GetFileName(repoPath)}");
                return CloneResult.AlreadyExists;
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
                ItemSkipped?.Invoke($"既にクローン済み: {repoName}");
                return CloneResult.AlreadyExists;
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
            return CloneResult.Cloned;
        }
        catch (Exception ex)
        {
            var msg = $"[AutoClone] クローン失敗: {repoPath} — {ex.Message}";
            GitLogService.Log(msg);
            ErrorOccurred?.Invoke(msg);
            return CloneResult.Error;
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

/// <summary>スキャン結果。</summary>
public class ScanResult
{
    public int Cloned { get; set; }
    public int Skipped { get; set; }
    public int Errors { get; set; }
    public int Total => Cloned + Skipped + Errors;
}

/// <summary>個別クローンの結果。</summary>
public enum CloneResult
{
    Cloned,
    AlreadyExists,
    Error,
}
