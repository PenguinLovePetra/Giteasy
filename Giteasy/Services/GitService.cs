using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Giteasy.Models;
using LibGit2Sharp;

namespace Giteasy.Services;

/// <summary>
/// LibGit2Sharp を使用した Git 操作のラッパーサービス。
/// すべてのメソッドは例外を呼び出し元に伝播させ、ViewModel 側で try-catch してダイアログ表示します。
/// </summary>
public class GitService
{
    private string? _repositoryPath;
    private string? _userName;
    private string? _userEmail;

    /// <summary>現在設定されているリポジトリのパス。</summary>
    public string? RepositoryPath => _repositoryPath;

    /// <summary>リポジトリが設定済みかどうか。</summary>
    public bool IsRepositorySet => !string.IsNullOrEmpty(_repositoryPath) && Repository.IsValid(_repositoryPath);

    /// <summary>現在のブランチ名を取得します。</summary>
    public string CurrentBranchName
    {
        get
        {
            if (!IsRepositorySet) return "未設定";
            using var repo = new Repository(_repositoryPath);
            return repo.Head?.FriendlyName ?? "不明";
        }
    }

    /// <summary>
    /// リポジトリのパスを設定します。
    /// </summary>
    public void SetRepository(string path)
    {
        // .git フォルダを含むルートを探す
        var discovered = Repository.Discover(path);
        if (discovered == null)
            throw new RepositoryNotFoundException($"指定されたパスにGitリポジトリが見つかりません:\n{path}");

        _repositoryPath = new DirectoryInfo(discovered).Parent?.FullName ?? discovered;
    }

    /// <summary>
    /// ユーザー名とメールを設定します。
    /// </summary>
    public void SetUser(string name, string email)
    {
        _userName = name;
        _userEmail = email;
    }

    /// <summary>
    /// ユーザー名を取得します（設定済みならそれを、なければリポジトリの設定を使用）。
    /// </summary>
    public (string Name, string Email) GetUser()
    {
        if (!string.IsNullOrEmpty(_userName) && !string.IsNullOrEmpty(_userEmail))
            return (_userName, _userEmail);

        if (IsRepositorySet)
        {
            using var repo = new Repository(_repositoryPath);
            var config = repo.Config;
            var name = config.Get<string>("user.name")?.Value ?? "";
            var email = config.Get<string>("user.email")?.Value ?? "";
            return (name, email);
        }
        return ("", "");
    }

    // ─── ステータス ─────────────────────────

    /// <summary>
    /// 変更されたファイルの一覧を取得します。
    /// </summary>
    public List<FileChange> GetChangedFiles()
    {
        EnsureRepository();
        using var repo = new Repository(_repositoryPath);
        var status = repo.RetrieveStatus(new StatusOptions());
        var changes = new List<FileChange>();

        foreach (var entry in status)
        {
            if (entry.State == FileStatus.Ignored)
                continue;
            changes.Add(new FileChange(entry.FilePath, entry.State));
        }
        return changes;
    }

    /// <summary>
    /// 指定されたファイルをステージングします。
    /// </summary>
    public void StageFiles(IEnumerable<string> filePaths)
    {
        EnsureRepository();
        using var repo = new Repository(_repositoryPath);
        foreach (var path in filePaths)
        {
            Commands.Stage(repo, path);
        }
    }

    /// <summary>
    /// すべての変更をステージングします。
    /// </summary>
    public void StageAll()
    {
        EnsureRepository();
        using var repo = new Repository(_repositoryPath);
        Commands.Stage(repo, "*");
    }

    /// <summary>
    /// コミットを実行します。
    /// </summary>
    public void Commit(string message)
    {
        EnsureRepository();
        if (string.IsNullOrWhiteSpace(message))
            throw new InvalidOperationException("コミットメッセージを入力してください。");

        using var repo = new Repository(_repositoryPath);
        var user = GetUser();
        if (string.IsNullOrEmpty(user.Name) || string.IsNullOrEmpty(user.Email))
            throw new InvalidOperationException("ユーザー名とメールアドレスを設定画面で設定してください。");

        var signature = new Signature(user.Name, user.Email, DateTimeOffset.Now);
        repo.Commit(message, signature, signature);
    }

    /// <summary>
    /// 指定されたファイルの変更を破棄します。
    /// </summary>
    public void DiscardChanges(IEnumerable<string> filePaths)
    {
        EnsureRepository();
        using var repo = new Repository(_repositoryPath);
        var options = new CheckoutOptions { CheckoutModifiers = CheckoutModifiers.Force };
        foreach (var path in filePaths)
        {
            repo.CheckoutPaths(repo.Head.FriendlyName, new[] { path }, options);
        }
    }

    // ─── ブランチ ─────────────────────────

    /// <summary>
    /// ブランチ一覧を取得します。
    /// </summary>
    public List<BranchInfo> GetBranches()
    {
        EnsureRepository();
        using var repo = new Repository(_repositoryPath);
        return repo.Branches
            .Select(b => new BranchInfo(b.FriendlyName, b.CanonicalName, b.IsCurrentRepositoryHead, b.IsRemote))
            .OrderByDescending(b => b.IsHead)
            .ThenBy(b => b.IsRemote)
            .ThenBy(b => b.Name)
            .ToList();
    }

    /// <summary>
    /// 新しいブランチを作成します。
    /// </summary>
    public void CreateBranch(string branchName)
    {
        EnsureRepository();
        using var repo = new Repository(_repositoryPath);
        repo.CreateBranch(branchName);
    }

    /// <summary>
    /// 指定されたブランチにチェックアウトします。
    /// </summary>
    public void Checkout(string branchName)
    {
        EnsureRepository();
        using var repo = new Repository(_repositoryPath);
        var branch = repo.Branches[branchName]
                     ?? throw new InvalidOperationException($"ブランチ '{branchName}' が見つかりません。");
        Commands.Checkout(repo, branch);
    }

    /// <summary>
    /// 指定されたブランチを現在のブランチにマージします。
    /// </summary>
    public MergeResult Merge(string branchName)
    {
        EnsureRepository();
        using var repo = new Repository(_repositoryPath);
        var branch = repo.Branches[branchName]
                     ?? throw new InvalidOperationException($"ブランチ '{branchName}' が見つかりません。");
        var user = GetUser();
        var signature = new Signature(user.Name, user.Email, DateTimeOffset.Now);
        return repo.Merge(branch, signature);
    }

    /// <summary>
    /// 指定されたローカルブランチを削除します。
    /// </summary>
    public void DeleteBranch(string branchName)
    {
        EnsureRepository();
        using var repo = new Repository(_repositoryPath);
        repo.Branches.Remove(branchName);
    }

    // ─── 同期 ─────────────────────────

    /// <summary>
    /// リモートからフェッチします。
    /// </summary>
    public async Task FetchAsync()
    {
        EnsureRepository();
        await Task.Run(() =>
        {
            using var repo = new Repository(_repositoryPath);
            foreach (var remote in repo.Network.Remotes)
            {
                var refSpecs = remote.FetchRefSpecs.Select(x => x.Specification).ToArray();
                Commands.Fetch(repo, remote.Name, refSpecs, null, "");
            }
        });
    }

    /// <summary>
    /// Pull を実行します。
    /// </summary>
    public async Task<MergeResult> PullAsync()
    {
        EnsureRepository();
        return await Task.Run(() =>
        {
            using var repo = new Repository(_repositoryPath);
            var user = GetUser();
            var signature = new Signature(user.Name, user.Email, DateTimeOffset.Now);
            var options = new PullOptions();
            return Commands.Pull(repo, signature, options);
        });
    }

    /// <summary>
    /// Push を実行します。
    /// </summary>
    public async Task PushAsync()
    {
        EnsureRepository();
        await Task.Run(() =>
        {
            using var repo = new Repository(_repositoryPath);
            var branch = repo.Head;
            if (branch.TrackedBranch == null)
                throw new InvalidOperationException(
                    "現在のブランチにはリモート追跡ブランチが設定されていません。\n" +
                    "まずは git push -u origin <branch> をコマンドラインで実行してください。");

            var remote = repo.Network.Remotes[branch.RemoteName]
                         ?? throw new InvalidOperationException("リモートリポジトリが見つかりません。");
            repo.Network.Push(remote, branch.CanonicalName);
        });
    }

    // ─── 履歴 ─────────────────────────

    /// <summary>
    /// コミット履歴を取得します。
    /// </summary>
    public List<CommitInfo> GetCommitLog(int maxCount = 100)
    {
        EnsureRepository();
        using var repo = new Repository(_repositoryPath);
        return repo.Commits
            .Take(maxCount)
            .Select(c => new CommitInfo(c.Sha, c.MessageShort, c.Author.Name, c.Author.When))
            .ToList();
    }

    /// <summary>
    /// 指定されたコミットを取り消すRevertを実行します。
    /// </summary>
    public RevertResult RevertCommit(string sha)
    {
        EnsureRepository();
        using var repo = new Repository(_repositoryPath);
        var commit = repo.Lookup<Commit>(sha)
                     ?? throw new InvalidOperationException($"コミット '{sha}' が見つかりません。");
        var user = GetUser();
        var signature = new Signature(user.Name, user.Email, DateTimeOffset.Now);
        return repo.Revert(commit, signature);
    }

    // ─── リポジトリセットアップ ──────────────────

    /// <summary>
    /// 新しいリポジトリを初期化し、オプションでリモートを設定します。
    /// </summary>
    public async Task InitRepositoryAsync(string localPath, string? remoteUrl = null)
    {
        await Task.Run(() =>
        {
            if (!Directory.Exists(localPath))
                Directory.CreateDirectory(localPath);

            Repository.Init(localPath);

            if (!string.IsNullOrWhiteSpace(remoteUrl))
            {
                using var repo = new Repository(localPath);
                repo.Network.Remotes.Add("origin", remoteUrl);
            }
        });

        _repositoryPath = localPath;
    }

    /// <summary>
    /// リモートリポジトリをクローンします。
    /// </summary>
    public async Task CloneRepositoryAsync(string remoteUrl, string localPath)
    {
        if (string.IsNullOrWhiteSpace(remoteUrl))
            throw new InvalidOperationException("リモートURLを入力してください。");
        if (string.IsNullOrWhiteSpace(localPath))
            throw new InvalidOperationException("ローカルパスを入力してください。");

        await Task.Run(() =>
        {
            var options = new CloneOptions();
            Repository.Clone(remoteUrl, localPath, options);
        });

        _repositoryPath = localPath;
    }

    // ─── ヘルパー ─────────────────────────

    private void EnsureRepository()
    {
        if (!IsRepositorySet)
            throw new InvalidOperationException(
                "リポジトリが設定されていません。\n設定画面からリポジトリのパスを指定してください。");
    }
}
