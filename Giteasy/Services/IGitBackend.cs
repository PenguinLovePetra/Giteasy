using System.Collections.Generic;
using System.Threading.Tasks;
using Giteasy.Models;

namespace Giteasy.Services;

/// <summary>
/// Git 操作の共通インターフェース。
/// LibGit2Sharp と git.exe の両方のバックエンドが実装します。
/// </summary>
public interface IGitBackend
{
    // ─── リポジトリ管理 ────────────────
    void SetRepository(string path);
    string? RepositoryPath { get; }
    bool IsRepositorySet { get; }
    string CurrentBranchName { get; }

    // ─── ユーザー ─────────────────────
    void SetUser(string name, string email);

    // ─── ステータス / ステージング ──────
    List<FileChange> GetChangedFiles();
    void StageFiles(IEnumerable<string> filePaths);
    void StageAll();
    void Commit(string message);
    void DiscardChanges(IEnumerable<string> filePaths);

    // ─── ブランチ ─────────────────────
    List<BranchInfo> GetBranches();
    void CreateBranch(string name);
    void CreateBranchFromCommit(string name, string commitSha);
    void Checkout(string branchName);
    string Merge(string sourceBranch);
    void DeleteBranch(string branchName);

    // ─── リモート操作 ─────────────────
    Task FetchAsync();
    Task<string> PullAsync();
    Task PushAsync();

    // ─── 履歴 ─────────────────────────
    List<CommitInfo> GetCommitLog(int maxCount = 100);
    string RevertCommit(string sha);

    // ─── セットアップ ─────────────────
    Task InitRepositoryAsync(string localPath, string? remoteUrl = null);
    Task CloneRepositoryAsync(string remoteUrl, string localPath);
}
