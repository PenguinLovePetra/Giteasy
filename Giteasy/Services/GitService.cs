using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Giteasy.Models;
using LibGit2Sharp;

namespace Giteasy.Services;

/// <summary>
/// Git 操作のメインサービス。LibGit2Sharp をデフォルトバックエンドとして使用し、
/// git.exe バックエンドに切替可能です。
/// </summary>
public class GitService : IGitBackend
{
    private string? _repositoryPath;
    private string? _userName;
    private string? _userEmail;
    private IGitBackend? _externalBackend;

    /// <summary>初期セットアップ時に生成する README.md のテンプレート。</summary>
    internal const string ReadmeContent = @"# プロジェクト名

このリポジトリは [GitEasy](https://github.com/PenguinLovePetra/Giteasy) で管理されています。

## GitEasy の使い方

### 基本操作
1. **ステータス確認** — 変更されたファイルの一覧を確認します
2. **ステージング** — コミットに含めたいファイルを選択します
3. **コミット** — メッセージを入力して変更を記録します
4. **同期（Push / Pull）** — リモートリポジトリと同期します

### ブランチ操作
- ブランチの作成・切替・マージ・削除が可能です

### 履歴
- コミット履歴の閲覧・Revert（取り消し）が可能です

---

## Git 用語ガイド（初心者向け）

| 用語 | 説明 |
|------|------|
| **リポジトリ（Repository）** | プロジェクトの全ファイルと変更履歴を保管する場所です |
| **コミット（Commit）** | ファイルの変更を「セーブポイント」として記録する操作です |
| **ステージング（Staging）** | コミットに含めるファイルを選ぶ準備段階です |
| **ブランチ（Branch）** | 本流から分岐して、別の作業を並行して進められる仕組みです |
| **マージ（Merge）** | ブランチで行った変更を別のブランチに統合する操作です |
| **プッシュ（Push）** | ローカルの変更をリモート（サーバー）にアップロードする操作です |
| **プル（Pull）** | リモート（サーバー）の変更をローカルにダウンロードする操作です |
| **クローン（Clone）** | リモートリポジトリをローカルにコピーする操作です |
| **リモート（Remote）** | サーバー上にあるリポジトリのことです。通常 `origin` という名前で管理します |
| **フェッチ（Fetch）** | リモートの更新情報を取得する操作です（ファイルはまだ反映されません） |
| **リバート（Revert）** | 過去のコミットを打ち消す新しいコミットを作成する操作です |
| **HEAD** | 現在チェックアウトしているブランチ・コミットを指す目印です |
| **main** | メインのブランチ名です。プロジェクトの正式な状態を表します |
";

    /// <summary>現在アクティブなバックエンドモード。</summary>
    public string ActiveBackendMode { get; private set; } = BackendModes.Builtin;

    /// <summary>
    /// バックエンドを切り替えます。
    /// </summary>
    /// <param name="mode">"builtin" (LibGit2Sharp) or "system" (git.exe)</param>
    public void SetBackend(string mode)
    {
        ActiveBackendMode = mode;
        if (mode == BackendModes.System && GitExeDetector.IsAvailable)
        {
            var backend = new GitExeBackend(GitExeDetector.DetectedPath!);
            // 現在の設定を引き継ぐ
            if (!string.IsNullOrEmpty(_repositoryPath))
            {
                try { backend.SetRepository(_repositoryPath); } catch { }
            }
            if (!string.IsNullOrEmpty(_userName))
                backend.SetUser(_userName!, _userEmail ?? "");
            _externalBackend = backend;
        }
        else
        {
            _externalBackend = null;
            ActiveBackendMode = BackendModes.Builtin;
        }
    }

    /// <summary>現在設定されているリポジトリのパス。</summary>
    public string? RepositoryPath => _externalBackend?.RepositoryPath ?? _repositoryPath;

    /// <summary>リポジトリが設定済みかどうか。</summary>
    public bool IsRepositorySet => _externalBackend != null
        ? _externalBackend.IsRepositorySet
        : (!string.IsNullOrEmpty(_repositoryPath) && Repository.IsValid(_repositoryPath));

    /// <summary>現在のブランチ名を取得します。</summary>
    public string CurrentBranchName
    {
        get
        {
            try
            {
                if (_externalBackend != null) return _externalBackend.CurrentBranchName;
                if (!IsRepositorySet) return "未設定";
                using var repo = new Repository(_repositoryPath);
                if (repo.Head == null || repo.Head.Reference == null)
                    return "初期状態（コミットなし）";
                return repo.Head.FriendlyName ?? "不明";
            }
            catch
            {
                return "不明";
            }
        }
    }

    /// <summary>
    /// リポジトリのパスを設定します。
    /// UNC パス（\\server\share\...）にも対応しています。
    /// </summary>
    public void SetRepository(string path)
    {
        if (_externalBackend != null) { _externalBackend.SetRepository(path); _repositoryPath = _externalBackend.RepositoryPath; return; }

        // Repository.Discover は UNC パスで失敗することがあるため、
        // 直接パスの妥当性を確認するフォールバックも用意
        var discovered = Repository.Discover(path);
        if (discovered != null)
        {
            // Discover は ".git/" のパスを返す (例: "\\server\share\repo\.git\")
            // 末尾のスラッシュと .git を除去してリポジトリルートを得る
            var trimmed = discovered.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (trimmed.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            {
                _repositoryPath = Path.GetDirectoryName(trimmed) ?? trimmed;
            }
            else
            {
                _repositoryPath = trimmed;
            }
            return;
        }

        // Discover 失敗時のフォールバック: 直接パスを検証
        var normalizedPath = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (Repository.IsValid(normalizedPath))
        {
            _repositoryPath = normalizedPath;
            return;
        }

        // .git サブディレクトリを含むパスも試行
        var gitSubDir = Path.Combine(normalizedPath, ".git");
        if (Directory.Exists(gitSubDir) && Repository.IsValid(normalizedPath))
        {
            _repositoryPath = normalizedPath;
            return;
        }

        throw new RepositoryNotFoundException($"指定されたパスにGitリポジトリが見つかりません:\n{path}");
    }

    /// <summary>
    /// ユーザー名とメールを設定します。
    /// </summary>
    public void SetUser(string name, string email)
    {
        _userName = name;
        _userEmail = email;
        _externalBackend?.SetUser(name, email);
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
        if (_externalBackend != null) return _externalBackend.GetChangedFiles();
        EnsureRepository();
        try
        {
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
        catch (Exception ex)
        {
            GitLogService.Log($"[ステータス取得エラー] {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 指定されたファイルをステージングします。
    /// </summary>
    public void StageFiles(IEnumerable<string> filePaths)
    {
        if (_externalBackend != null) { _externalBackend.StageFiles(filePaths); return; }
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
        if (_externalBackend != null) { _externalBackend.StageAll(); return; }
        EnsureRepository();
        using var repo = new Repository(_repositoryPath);
        Commands.Stage(repo, "*");
    }

    /// <summary>
    /// コミットを実行します。
    /// </summary>
    public void Commit(string message)
    {
        if (_externalBackend != null) { _externalBackend.Commit(message); return; }
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
        if (_externalBackend != null) { _externalBackend.DiscardChanges(filePaths); return; }
        EnsureRepository();
        using var repo = new Repository(_repositoryPath);

        // 初期状態（コミットなし）の場合、checkoutできない
        if (repo.Head?.Tip == null)
            throw new InvalidOperationException("コミットがまだありません。変更を手動で削除してください。");

        var options = new CheckoutOptions { CheckoutModifiers = CheckoutModifiers.Force };
        foreach (var path in filePaths)
        {
            repo.CheckoutPaths(repo.Head.FriendlyName, new[] { path }, options);
        }
    }

    // ─── ブランチ ─────────────────────────

    /// <summary>
    /// ブランチ一覧を取得します。
    /// ローカルブランチと同期済みのリモートブランチは統合して表示します。
    /// </summary>
    public List<BranchInfo> GetBranches()
    {
        if (_externalBackend != null) return _externalBackend.GetBranches();
        EnsureRepository();
        using var repo = new Repository(_repositoryPath);

        // ローカルブランチのトラッキング情報を収集
        var trackedRemoteNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var localBranches = new List<BranchInfo>();

        foreach (var b in repo.Branches.Where(b => !b.IsRemote))
        {
            var trackedName = b.TrackedBranch?.FriendlyName;
            if (!string.IsNullOrEmpty(trackedName))
                trackedRemoteNames.Add(trackedName);
            localBranches.Add(new BranchInfo(
                b.FriendlyName, b.CanonicalName,
                b.IsCurrentRepositoryHead, false, trackedName));
        }

        // リモートブランチ（同期済みのものは除外）
        var remoteBranches = repo.Branches
            .Where(b => b.IsRemote && !trackedRemoteNames.Contains(b.FriendlyName))
            .Select(b => new BranchInfo(b.FriendlyName, b.CanonicalName, false, true))
            .ToList();

        return localBranches
            .Concat(remoteBranches)
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
        if (_externalBackend != null) { _externalBackend.CreateBranch(branchName); return; }
        EnsureRepository();
        using var repo = new Repository(_repositoryPath);
        repo.CreateBranch(branchName);
    }

    /// <summary>
    /// 指定されたブランチにチェックアウトします。
    /// </summary>
    public void Checkout(string branchName)
    {
        if (_externalBackend != null) { _externalBackend.Checkout(branchName); return; }
        EnsureRepository();
        using var repo = new Repository(_repositoryPath);
        var branch = repo.Branches[branchName]
                     ?? throw new InvalidOperationException($"ブランチ '{branchName}' が見つかりません。");
        Commands.Checkout(repo, branch);
    }

    /// <summary>
    /// 指定されたブランチを現在のブランチにマージします。
    /// </summary>
    public string Merge(string branchName)
    {
        if (_externalBackend != null) return _externalBackend.Merge(branchName);
        EnsureRepository();
        using var repo = new Repository(_repositoryPath);
        var branch = repo.Branches[branchName]
                     ?? throw new InvalidOperationException($"ブランチ '{branchName}' が見つかりません。");
        var user = GetUser();
        var signature = new Signature(user.Name, user.Email, DateTimeOffset.Now);
        var result = repo.Merge(branch, signature);
        return result.Status.ToString();
    }

    /// <summary>
    /// 指定されたローカルブランチを削除します。
    /// </summary>
    public void DeleteBranch(string branchName)
    {
        if (_externalBackend != null) { _externalBackend.DeleteBranch(branchName); return; }
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
        if (_externalBackend != null) { await _externalBackend.FetchAsync(); return; }
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
    public async Task<string> PullAsync()
    {
        if (_externalBackend != null) return await _externalBackend.PullAsync();
        EnsureRepository();
        return await Task.Run(() =>
        {
            using var repo = new Repository(_repositoryPath);
            var user = GetUser();
            var signature = new Signature(user.Name, user.Email, DateTimeOffset.Now);
            var options = new PullOptions();
            var result = Commands.Pull(repo, signature, options);
            return result.Status.ToString();
        });
    }

    /// <summary>
    /// Push を実行します。トラッキングブランチ未設定時は自動で origin に push -u 相当を行います。
    /// </summary>
    public async Task PushAsync()
    {
        if (_externalBackend != null) { await _externalBackend.PushAsync(); return; }
        EnsureRepository();
        await Task.Run(() =>
        {
            using var repo = new Repository(_repositoryPath);
            var branch = repo.Head;

            if (branch.TrackedBranch != null)
            {
                // トラッキングブランチあり → 通常Push
                var remote = repo.Network.Remotes[branch.RemoteName]
                             ?? throw new InvalidOperationException("リモートリポジトリが見つかりません。");
                repo.Network.Push(remote, branch.CanonicalName);
                return;
            }

            // トラッキングブランチなし → origin に自動Push + upstream設定
            var origin = repo.Network.Remotes["origin"]
                         ?? throw new InvalidOperationException(
                             "リモート 'origin' が設定されていません。\nセットアップ画面でリモートURLを設定してください。");

            // リモートがローカルパスの場合、bare リポジトリを自動初期化
            EnsureRemoteBareIfLocal(origin.Url);

            repo.Network.Push(origin, branch.CanonicalName);

            // upstream (トラッキング) を設定
            repo.Branches.Update(branch,
                b => b.Remote = "origin",
                b => b.UpstreamBranch = branch.CanonicalName);
        });
    }

    /// <summary>
    /// リモートURLがローカルパスの場合、bare リポジトリが存在しなければ自動初期化します。
    /// HEAD は refs/heads/main に設定されます。
    /// </summary>
    private static void EnsureRemoteBareIfLocal(string remoteUrl)
    {
        // URL (http/ssh/git://) はスキップ
        if (remoteUrl.Contains("://") || remoteUrl.Contains("@")) return;

        // クォート除去 & ローカルパスまたは UNC パス
        var remotePath = remoteUrl.Trim().Trim('"');
        if (!Directory.Exists(remotePath))
        {
            Directory.CreateDirectory(remotePath);
            Repository.Init(remotePath, isBare: true);
            // HEAD を main に設定
            using var repo = new Repository(remotePath);
            repo.Refs.UpdateTarget("HEAD", "refs/heads/main");
            return;
        }

        // ディレクトリは存在するが Git リポジトリでない場合
        if (!Repository.IsValid(remotePath))
        {
            Repository.Init(remotePath, isBare: true);
            // HEAD を main に設定
            using var repo = new Repository(remotePath);
            repo.Refs.UpdateTarget("HEAD", "refs/heads/main");
        }
    }

    // ─── 履歴 ─────────────────────────

    /// <summary>
    /// コミット履歴を取得します。
    /// </summary>
    public List<CommitInfo> GetCommitLog(int maxCount = 100)
    {
        if (_externalBackend != null) return _externalBackend.GetCommitLog(maxCount);
        EnsureRepository();
        using var repo = new Repository(_repositoryPath);

        // Refマッピングを構築（SHA → Ref名リスト）
        var refMap = new Dictionary<string, List<string>>();
        foreach (var r in repo.Refs)
        {
            var targetSha = r.ResolveToDirectReference()?.Target?.Sha;
            if (targetSha == null) continue;
            if (!refMap.ContainsKey(targetSha))
                refMap[targetSha] = new List<string>();
            // フレンドリー名に変換
            var name = r.CanonicalName;
            if (name.StartsWith("refs/heads/")) name = name["refs/heads/".Length..];
            else if (name.StartsWith("refs/tags/")) name = "tag: " + name["refs/tags/".Length..];
            else if (name == "HEAD") name = "HEAD";
            else continue; // remotes等はスキップ
            refMap[targetSha].Add(name);
        }

        return repo.Commits
            .Take(maxCount)
            .Select(c => new CommitInfo(
                c.Sha,
                c.MessageShort,
                c.Author.Name,
                c.Author.When,
                c.Parents.Select(p => p.Sha).ToList(),
                refMap.TryGetValue(c.Sha, out var refs) ? refs : null))
            .ToList();
    }

    /// <summary>
    /// 指定されたコミットを取り消すRevertを実行します。
    /// </summary>
    public string RevertCommit(string sha)
    {
        if (_externalBackend != null) return _externalBackend.RevertCommit(sha);
        EnsureRepository();
        using var repo = new Repository(_repositoryPath);
        var commit = repo.Lookup<Commit>(sha)
                     ?? throw new InvalidOperationException($"コミット '{sha}' が見つかりません。");
        var user = GetUser();
        var signature = new Signature(user.Name, user.Email, DateTimeOffset.Now);
        var result = repo.Revert(commit, signature);
        return result.Status.ToString();
    }

    // ─── リポジトリセットアップ ──────────────────

    /// <summary>IGitBackend の明示的実装（autoInitBare=true で転送）。</summary>
    async Task IGitBackend.InitRepositoryAsync(string localPath, string? remoteUrl)
        => await InitRepositoryAsync(localPath, remoteUrl, true);

    /// <summary>
    /// 新しいリポジトリを初期化し、オプションでリモートを設定します。
    /// 初期ブランチを main に設定し、README.md を生成して初回コミット＆Pushを行います。
    /// リモートがローカルパスの場合、bare リポジトリを自動生成します。
    /// </summary>
    public async Task InitRepositoryAsync(string localPath, string? remoteUrl = null, bool autoInitBare = true)
    {
        if (_externalBackend != null) { await _externalBackend.InitRepositoryAsync(localPath, remoteUrl); _repositoryPath = localPath; return; }
        await Task.Run(() =>
        {
            if (!Directory.Exists(localPath))
                Directory.CreateDirectory(localPath);

            Repository.Init(localPath);
            GitLogService.Log($"init: {localPath}");

            // HEAD を main ブランチに変更（デフォルトの master → main）
            using (var repo = new Repository(localPath))
            {
                repo.Refs.UpdateTarget("HEAD", "refs/heads/main");
                GitLogService.Log("HEAD → refs/heads/main");
            }

            // README.md を生成
            var readmePath = Path.Combine(localPath, "README.md");
            if (!File.Exists(readmePath))
            {
                File.WriteAllText(readmePath, ReadmeContent);
                GitLogService.Log("README.md 生成完了");
            }

            // リモートURL設定
            if (!string.IsNullOrWhiteSpace(remoteUrl))
            {
                if (autoInitBare)
                    EnsureRemoteBareIfLocal(remoteUrl);

                using var repo = new Repository(localPath);
                var existing = repo.Network.Remotes["origin"];
                if (existing != null)
                    repo.Network.Remotes.Update("origin", r => r.Url = remoteUrl);
                else
                    repo.Network.Remotes.Add("origin", remoteUrl);
                GitLogService.Log($"remote add origin {remoteUrl}");
            }

            // 初回コミット（README.md をステージングしてコミット）
            using (var repo = new Repository(localPath))
            {
                Commands.Stage(repo, "README.md");
                var user = GetUser();
                var name = string.IsNullOrEmpty(user.Name) ? "GitEasy User" : user.Name;
                var email = string.IsNullOrEmpty(user.Email) ? "giteasy@local" : user.Email;
                var signature = new Signature(name, email, DateTimeOffset.Now);
                repo.Commit("Initial commit", signature, signature);
                GitLogService.Log("Initial commit 完了");
            }

            // 初回Push（リモートが設定されている場合）
            if (!string.IsNullOrWhiteSpace(remoteUrl))
            {
                try
                {
                    using var repo = new Repository(localPath);
                    var origin = repo.Network.Remotes["origin"];
                    if (origin != null)
                    {
                        repo.Network.Push(origin, "refs/heads/main");
                        // upstream を設定
                        var branch = repo.Branches["main"];
                        if (branch != null)
                        {
                            repo.Branches.Update(branch,
                                b => b.Remote = "origin",
                                b => b.UpstreamBranch = "refs/heads/main");
                        }
                        GitLogService.Log("初回 Push 完了 (origin/main)");
                    }
                }
                catch (Exception ex)
                {
                    // Push失敗はセットアップ全体を失敗にしない（後からPush可能）
                    GitLogService.Log($"[初回Push警告] {ex.Message}");
                }
            }
        });

        _repositoryPath = localPath;
    }

    /// <summary>
    /// リモートリポジトリをクローンします。
    /// </summary>
    public async Task CloneRepositoryAsync(string remoteUrl, string localPath)
    {
        if (_externalBackend != null) { await _externalBackend.CloneRepositoryAsync(remoteUrl, localPath); _repositoryPath = localPath; return; }
        if (string.IsNullOrWhiteSpace(remoteUrl))
            throw new InvalidOperationException("リモートURLを入力してください。");
        if (string.IsNullOrWhiteSpace(localPath))
            throw new InvalidOperationException("ローカルパスを入力してください。");

        await Task.Run(() =>
        {
            // safe.directory に登録（ネットワーク共有フォルダ等での所有権エラーを防止）
            try
            {
                var safeDir = new SafeDirectoryService();
                safeDir.AddSafeDirectory(remoteUrl);
                safeDir.AddSafeDirectory(localPath);
            }
            catch (Exception ex)
            {
                GitLogService.Log($"[SafeDirectory] 自動登録の警告: {ex.Message}");
            }

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
