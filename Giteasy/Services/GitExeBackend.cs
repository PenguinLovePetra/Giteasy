using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Giteasy.Models;
using LibGit2Sharp;

namespace Giteasy.Services;

/// <summary>
/// git.exe を Process.Start で呼び出す Git バックエンド。
/// SSH認証やクレデンシャルヘルパーが自動的に利用されます。
/// </summary>
public class GitExeBackend : IGitBackend
{
    private string? _repositoryPath;
    private string? _userName;
    private string? _userEmail;
    private readonly string _gitExePath;

    public GitExeBackend(string gitExePath)
    {
        _gitExePath = gitExePath;
    }

    public string? RepositoryPath => _repositoryPath;

    public bool IsRepositorySet
    {
        get
        {
            if (string.IsNullOrEmpty(_repositoryPath)) return false;
            try
            {
                var result = RunGit("rev-parse --git-dir", _repositoryPath);
                return result.ExitCode == 0;
            }
            catch { return false; }
        }
    }

    public string CurrentBranchName
    {
        get
        {
            if (!IsRepositorySet) return "未設定";
            var result = RunGit("rev-parse --abbrev-ref HEAD", _repositoryPath!);
            return result.ExitCode == 0 ? result.Output.Trim() : "不明";
        }
    }

    public void SetRepository(string path)
    {
        var result = RunGit("rev-parse --show-toplevel", path);
        if (result.ExitCode != 0)
            throw new InvalidOperationException($"指定されたパスにGitリポジトリが見つかりません:\n{path}");
        _repositoryPath = result.Output.Trim().Replace('/', Path.DirectorySeparatorChar);
    }

    public void SetUser(string name, string email)
    {
        _userName = name;
        _userEmail = email;
    }

    public List<FileChange> GetChangedFiles()
    {
        EnsureRepository();
        var result = RunGit("status --porcelain", _repositoryPath!);
        if (result.ExitCode != 0) return new List<FileChange>();

        var files = new List<FileChange>();
        foreach (var line in result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Length < 3) continue;
            var statusCode = line[..2].Trim();
            var filePath = line[3..].Trim().Trim('"');

            // porcelain ステータスを LibGit2Sharp の FileStatus にマッピング
            // 形式: XY (X=インデックス, Y=ワーキングツリー)
            var fileStatus = statusCode switch
            {
                // インデックス変更 + ワーキングツリー変更
                "MM" => FileStatus.ModifiedInWorkdir,
                "AM" => FileStatus.NewInIndex,
                // インデックスのみ変更（ステージ済み）
                "M" => FileStatus.ModifiedInIndex,
                "A" => FileStatus.NewInIndex,
                "D" => FileStatus.DeletedFromIndex,
                "R" => FileStatus.RenamedInIndex,
                // ワーキングツリーのみ変更（未ステージ）
                " M" => FileStatus.ModifiedInWorkdir,
                " D" => FileStatus.DeletedFromWorkdir,
                // 追跡外
                "??" => FileStatus.NewInWorkdir,
                // インデックス変更 + ワーキングツリー削除
                "MD" => FileStatus.ModifiedInWorkdir,
                "AD" => FileStatus.NewInIndex,
                _ => FileStatus.Unaltered,
            };

            if (fileStatus != FileStatus.Unaltered)
                files.Add(new FileChange(filePath, fileStatus));
        }
        return files;
    }

    public void StageFiles(IEnumerable<string> filePaths)
    {
        EnsureRepository();
        foreach (var p in filePaths)
            RunGitOrThrow($"add \"{p}\"", _repositoryPath!);
    }

    public void StageAll()
    {
        EnsureRepository();
        RunGitOrThrow("add -A", _repositoryPath!);
    }

    public void Commit(string message)
    {
        EnsureRepository();
        if (string.IsNullOrWhiteSpace(message))
            throw new InvalidOperationException("コミットメッセージを入力してください。");

        // ユーザー設定
        if (!string.IsNullOrWhiteSpace(_userName))
        {
            RunGit($"config user.name \"{_userName}\"", _repositoryPath!);
            RunGit($"config user.email \"{_userEmail}\"", _repositoryPath!);
        }

        RunGitOrThrow($"commit -m \"{message.Replace("\"", "\\\"")}\"", _repositoryPath!);
    }

    public void DiscardChanges(IEnumerable<string> filePaths)
    {
        EnsureRepository();
        foreach (var p in filePaths)
            RunGitOrThrow($"checkout -- \"{p}\"", _repositoryPath!);
    }

    public List<BranchInfo> GetBranches()
    {
        EnsureRepository();
        var branches = new List<BranchInfo>();

        // ローカルブランチ
        var local = RunGit("branch --format=%(refname:short)|%(refname)|%(HEAD)", _repositoryPath!);
        if (local.ExitCode == 0)
        {
            foreach (var line in local.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = line.Split('|');
                if (parts.Length < 3) continue;
                branches.Add(new BranchInfo(parts[0].Trim(), parts[1].Trim(),
                    parts[2].Trim() == "*", false));
            }
        }

        // リモートブランチ
        var remote = RunGit("branch -r --format=%(refname:short)|%(refname)", _repositoryPath!);
        if (remote.ExitCode == 0)
        {
            foreach (var line in remote.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = line.Split('|');
                if (parts.Length < 2) continue;
                if (parts[0].Trim().Contains("HEAD")) continue;
                branches.Add(new BranchInfo(parts[0].Trim(), parts[1].Trim(), false, true));
            }
        }

        return branches;
    }

    public void CreateBranch(string name)
    {
        EnsureRepository();
        RunGitOrThrow($"branch \"{name}\"", _repositoryPath!);
    }

    public void Checkout(string branchName)
    {
        EnsureRepository();
        RunGitOrThrow($"checkout \"{branchName}\"", _repositoryPath!);
    }

    public string Merge(string sourceBranch)
    {
        EnsureRepository();
        if (!string.IsNullOrWhiteSpace(_userName))
        {
            RunGit($"config user.name \"{_userName}\"", _repositoryPath!);
            RunGit($"config user.email \"{_userEmail}\"", _repositoryPath!);
        }
        var result = RunGit($"merge \"{sourceBranch}\"", _repositoryPath!);
        return result.ExitCode == 0 ? "成功" : result.Error;
    }

    public void DeleteBranch(string branchName)
    {
        EnsureRepository();
        RunGitOrThrow($"branch -d \"{branchName}\"", _repositoryPath!);
    }

    public async Task FetchAsync()
    {
        EnsureRepository();
        await Task.Run(() => RunGitOrThrow("fetch --all", _repositoryPath!));
    }

    public async Task<string> PullAsync()
    {
        EnsureRepository();
        return await Task.Run(() =>
        {
            if (!string.IsNullOrWhiteSpace(_userName))
            {
                RunGit($"config user.name \"{_userName}\"", _repositoryPath!);
                RunGit($"config user.email \"{_userEmail}\"", _repositoryPath!);
            }
            var result = RunGit("pull", _repositoryPath!);
            if (result.ExitCode != 0)
                throw new InvalidOperationException(result.Error);
            return result.Output.Contains("Already up to date") ? "UpToDate" : "Merged";
        });
    }

    public async Task PushAsync()
    {
        EnsureRepository();
        await Task.Run(() =>
        {
            // まず通常 push を試行
            var result = RunGit("push", _repositoryPath!);
            if (result.ExitCode == 0) return;

            // upstream 未設定なら -u origin <branch> で再試行
            var branchResult = RunGit("rev-parse --abbrev-ref HEAD", _repositoryPath!);
            var branch = branchResult.Output.Trim();

            // リモートがローカルパスなら bare を自動初期化
            var remoteResult = RunGit("remote get-url origin", _repositoryPath!);
            if (remoteResult.ExitCode == 0)
                EnsureRemoteBareIfLocal(remoteResult.Output.Trim());

            var pushResult = RunGit($"push -u origin \"{branch}\"", _repositoryPath!);
            if (pushResult.ExitCode != 0)
                throw new InvalidOperationException(pushResult.Error);
        });
    }

    public List<CommitInfo> GetCommitLog(int maxCount = 100)
    {
        EnsureRepository();
        // パイプ文字がメッセージに入る可能性を考慮して区切りを特殊文字に
        var sep = "§§";
        var result = RunGit($"log -{maxCount} --format=%H{sep}%s{sep}%an{sep}%aI{sep}%P{sep}%D", _repositoryPath!);
        if (result.ExitCode != 0) return new List<CommitInfo>();

        var commits = new List<CommitInfo>();
        foreach (var line in result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split(sep, 6, StringSplitOptions.None);
            if (parts.Length < 6) continue;

            var parentShas = parts[4].Trim()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .ToList();

            var refsRaw = parts[5].Trim();
            var refs = string.IsNullOrEmpty(refsRaw)
                ? new List<string>()
                : refsRaw.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(r => r.Trim()
                        .Replace("HEAD -> ", "HEAD, ")  // "HEAD -> main" を分離
                    )
                    .SelectMany(r => r.Split(',', StringSplitOptions.RemoveEmptyEntries))
                    .Select(r => r.Trim())
                    .Where(r => !r.StartsWith("origin/")) // リモートブランチはスキップ
                    .ToList();

            commits.Add(new CommitInfo(
                parts[0].Trim(),
                parts[1].Trim(),
                parts[2].Trim(),
                DateTimeOffset.TryParse(parts[3].Trim(), out var dt)
                    ? dt : DateTimeOffset.MinValue,
                parentShas,
                refs));
        }
        return commits;
    }

    public string RevertCommit(string sha)
    {
        EnsureRepository();
        if (!string.IsNullOrWhiteSpace(_userName))
        {
            RunGit($"config user.name \"{_userName}\"", _repositoryPath!);
            RunGit($"config user.email \"{_userEmail}\"", _repositoryPath!);
        }
        var result = RunGit($"revert --no-edit {sha}", _repositoryPath!);
        if (result.ExitCode != 0)
            throw new InvalidOperationException(result.Error);
        return "Reverted";
    }

    public async Task InitRepositoryAsync(string localPath, string? remoteUrl = null)
    {
        var safePath = localPath.Trim().Trim('"');
        var safeUrl = remoteUrl?.Trim().Trim('"');
        await Task.Run(() =>
        {
            if (!Directory.Exists(safePath))
                Directory.CreateDirectory(safePath);

            // 初期ブランチを main に設定
            RunGitOrThrow("init -b main", safePath);

            // README.md を生成
            var readmePath = Path.Combine(safePath, "README.md");
            if (!File.Exists(readmePath))
                File.WriteAllText(readmePath, GitService.ReadmeContent);

            // リモートURL設定
            if (!string.IsNullOrWhiteSpace(safeUrl))
            {
                EnsureRemoteBareIfLocal(safeUrl);
                var check = RunGit("remote get-url origin", safePath);
                if (check.ExitCode == 0)
                    RunGitOrThrow($"remote set-url origin \"{safeUrl}\"", safePath);
                else
                    RunGitOrThrow($"remote add origin \"{safeUrl}\"", safePath);
            }

            // ユーザー設定（コミットに必要）
            var userName = _userName ?? "GitEasy User";
            var userEmail = _userEmail ?? "giteasy@local";
            RunGit($"config user.name \"{userName}\"", safePath);
            RunGit($"config user.email \"{userEmail}\"", safePath);

            // 初回コミット
            RunGitOrThrow("add README.md", safePath);
            RunGitOrThrow("commit -m \"Initial commit\"", safePath);

            // 初回Push（リモートが設定されている場合）
            if (!string.IsNullOrWhiteSpace(safeUrl))
            {
                var pushResult = RunGit("push -u origin main", safePath);
                if (pushResult.ExitCode != 0)
                    GitLogService.Log($"[初回Push警告] {pushResult.Error}");
            }
        });
        _repositoryPath = safePath;
    }

    public async Task CloneRepositoryAsync(string remoteUrl, string localPath)
    {
        if (string.IsNullOrWhiteSpace(remoteUrl))
            throw new InvalidOperationException("リモートURLを入力してください。");
        if (string.IsNullOrWhiteSpace(localPath))
            throw new InvalidOperationException("ローカルパスを入力してください。");

        await Task.Run(() =>
        {
            // URLサニタイズ: BOM・不可視文字・全角文字・引用符を除去
            var trimmedUrl = SanitizeUrl(remoteUrl.Trim().Trim('"'));
            var trimmedPath = localPath.Trim().Trim('"');

            var parent = Path.GetDirectoryName(trimmedPath) ?? trimmedPath;
            if (!Directory.Exists(parent))
                Directory.CreateDirectory(parent);

            // ローカルパスやUNCパスの場合、bareリポジトリを自動初期化
            EnsureRemoteBareIfLocal(trimmedUrl);

            // ローカルパスを git が正しく認識できる形式に変換
            // git は "C:\path" や "C:/path" を SSH URL (host:path) と誤解するため、
            // file:/// プロトコルを明示的に付与する
            var gitUrl = trimmedUrl;
            if (!gitUrl.Contains("://") && !gitUrl.Contains("@"))
            {
                // バックスラッシュ → フォワードスラッシュ
                gitUrl = gitUrl.Replace('\\', '/');

                if (gitUrl.Length >= 2 && gitUrl[1] == ':')
                {
                    // ドライブレター付きパス (例: C:/Users/...) → file:///C:/Users/...
                    gitUrl = "file:///" + gitUrl;
                }
                else if (gitUrl.StartsWith("//"))
                {
                    // UNCパス (例: //server/share) → file://server/share
                    gitUrl = "file:" + gitUrl;
                }
            }

            // ArgumentList を使ってパス内のスペースを安全に処理
            var psi = new ProcessStartInfo
            {
                FileName = _gitExePath,
                WorkingDirectory = parent,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };
            psi.ArgumentList.Add("clone");
            psi.ArgumentList.Add(gitUrl);
            psi.ArgumentList.Add(trimmedPath);

            GitLogService.Log($"$ git clone \"{gitUrl}\" \"{trimmedPath}\"");

            using var proc = Process.Start(psi)
                             ?? throw new InvalidOperationException("git.exe の起動に失敗しました。");

            // デッドロック防止: 出力を非同期で読み取ってからWaitForExit
            var outputTask = proc.StandardOutput.ReadToEndAsync();
            var errorTask = proc.StandardError.ReadToEndAsync();
            proc.WaitForExit(120000); // クローンは時間がかかるため2分
            var output = outputTask.Result;
            var error = errorTask.Result;

            if (!string.IsNullOrWhiteSpace(output))
                GitLogService.Log($"  {output.Trim()}");

            if (proc.ExitCode != 0)
            {
                if (!string.IsNullOrWhiteSpace(error))
                    GitLogService.Log($"  [ERROR] {error.Trim()}");

                // ユーザーに分かりやすいエラーメッセージを生成
                var userMessage = FormatCloneError(error, gitUrl);
                throw new InvalidOperationException(userMessage);
            }

            // 成功時のstderrもログ出力（progressメッセージなど）
            if (!string.IsNullOrWhiteSpace(error))
                GitLogService.Log($"  {error.Trim()}");
        });
        _repositoryPath = localPath;
    }

    // ─── ヘルパー ──────────────────────

    private void EnsureRemoteBareIfLocal(string remoteUrl)
    {
        if (remoteUrl.Contains("://") || remoteUrl.Contains("@")) return;
        remoteUrl = remoteUrl.Trim().Trim('"');
        if (!Directory.Exists(remoteUrl))
        {
            Directory.CreateDirectory(remoteUrl);
            var psi = new ProcessStartInfo(_gitExePath, "init --bare")
            {
                WorkingDirectory = remoteUrl,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var p = Process.Start(psi);
            p?.WaitForExit(10000);
            // HEAD を main に設定
            RunGit("symbolic-ref HEAD refs/heads/main", remoteUrl);
            return;
        }
        var check = new ProcessStartInfo(_gitExePath, "rev-parse --git-dir")
        {
            WorkingDirectory = remoteUrl,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var cp = Process.Start(check);
        if (cp != null)
        {
            cp.StandardOutput.ReadToEnd();
            cp.StandardError.ReadToEnd();
            cp.WaitForExit(5000);
        }
        if (cp?.ExitCode != 0)
        {
            var init = new ProcessStartInfo(_gitExePath, "init --bare")
            {
                WorkingDirectory = remoteUrl,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var ip = Process.Start(init);
            ip?.WaitForExit(10000);
            // HEAD を main に設定
            RunGit("symbolic-ref HEAD refs/heads/main", remoteUrl);
        }
    }

    private void EnsureRepository()
    {
        if (string.IsNullOrEmpty(_repositoryPath))
            throw new InvalidOperationException("リポジトリが設定されていません。");
    }

    private (int ExitCode, string Output, string Error) RunGit(string arguments, string workingDir)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _gitExePath,
            Arguments = arguments,
            WorkingDirectory = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        using var proc = Process.Start(psi)
                         ?? throw new InvalidOperationException("git.exe の起動に失敗しました。");

        // デッドロック防止: 出力を非同期で読み取ってからWaitForExit
        var outputTask = proc.StandardOutput.ReadToEndAsync();
        var errorTask = proc.StandardError.ReadToEndAsync();
        proc.WaitForExit(30000);
        var output = outputTask.Result;
        var error = errorTask.Result;

        // ログ記録
        GitLogService.Log($"$ git {arguments}");
        if (!string.IsNullOrWhiteSpace(output))
            GitLogService.Log($"  {output.Trim().Replace("\n", "\n  ")}");
        if (proc.ExitCode != 0 && !string.IsNullOrWhiteSpace(error))
            GitLogService.Log($"  [ERROR] {error.Trim()}");

        return (proc.ExitCode, output, error);
    }

    private void RunGitOrThrow(string arguments, string workingDir)
    {
        var result = RunGit(arguments, workingDir);
        if (result.ExitCode != 0)
            throw new InvalidOperationException(
                $"git {arguments.Split(' ')[0]} に失敗しました:\n{result.Error}");
    }

    /// <summary>URLから不可視文字・BOM・全角文字を除去します。</summary>
    private static string SanitizeUrl(string url)
    {
        if (string.IsNullOrEmpty(url)) return url;

        // BOM除去
        url = url.TrimStart('\uFEFF', '\u200B', '\u200C', '\u200D', '\uFEFF');

        // 全角英数字 → 半角英数字 への変換
        var sb = new System.Text.StringBuilder(url.Length);
        foreach (var c in url)
        {
            if (c >= '！' && c <= '～')
            {
                // 全角ASCII → 半角ASCII（0xFF01-0xFF5E → 0x0021-0x007E）
                sb.Append((char)(c - 0xFEE0));
            }
            else if (c == '　') // 全角スペース
            {
                sb.Append(' ');
            }
            else if (!char.IsControl(c) || c == '\t')
            {
                sb.Append(c);
            }
            // 制御文字はスキップ
        }

        return sb.ToString().Trim();
    }

    /// <summary>クローンエラーメッセージを日本語で分かりやすく変換します。</summary>
    private static string FormatCloneError(string error, string url)
    {
        if (string.IsNullOrWhiteSpace(error))
            return $"git clone に失敗しました。URL: {url}";

        if (error.Contains("hostname contains invalid characters"))
            return $"URLに不正な文字が含まれています。URLを確認してください。\n\n入力URL: {url}\n\n詳細: {error.Trim()}";
        if (error.Contains("Could not resolve host"))
            return $"ホスト名を解決できません。URLが正しいか、ネットワーク接続を確認してください。\n\n入力URL: {url}\n\n詳細: {error.Trim()}";
        if (error.Contains("Permission denied") || error.Contains("access rights"))
            return $"アクセス権限がありません。認証情報やSSHキーを確認してください。\n\n入力URL: {url}\n\n詳細: {error.Trim()}";
        if (error.Contains("repository not found") || error.Contains("does not exist"))
            return $"リポジトリが見つかりません。URLが正しいか確認してください。\n\n入力URL: {url}\n\n詳細: {error.Trim()}";

        return $"クローンに失敗しました。\n\n入力URL: {url}\n\n詳細: {error.Trim()}";
    }
}
