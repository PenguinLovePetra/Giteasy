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
            var fileStatus = statusCode switch
            {
                "M" => FileStatus.ModifiedInWorkdir,
                "MM" => FileStatus.ModifiedInWorkdir,
                "A" => FileStatus.NewInIndex,
                "AM" => FileStatus.NewInIndex,
                "D" => FileStatus.DeletedFromWorkdir,
                "R" => FileStatus.RenamedInWorkdir,
                "??" => FileStatus.NewInWorkdir,
                " M" => FileStatus.ModifiedInWorkdir,
                " D" => FileStatus.DeletedFromWorkdir,
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
        var result = RunGit($"log -{maxCount} --format=%H{sep}%s{sep}%an{sep}%aI", _repositoryPath!);
        if (result.ExitCode != 0) return new List<CommitInfo>();

        var commits = new List<CommitInfo>();
        foreach (var line in result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split(sep, 4, StringSplitOptions.None);
            if (parts.Length < 4) continue;
            commits.Add(new CommitInfo(
                parts[0].Trim(),
                parts[1].Trim(),
                parts[2].Trim(),
                DateTimeOffset.TryParse(parts[3].Trim(), out var dt)
                    ? dt : DateTimeOffset.MinValue));
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
        await Task.Run(() =>
        {
            if (!Directory.Exists(localPath))
                Directory.CreateDirectory(localPath);

            RunGitOrThrow("init", localPath);

            if (!string.IsNullOrWhiteSpace(remoteUrl))
            {
                EnsureRemoteBareIfLocal(remoteUrl);
                var check = RunGit("remote get-url origin", localPath);
                if (check.ExitCode == 0)
                    RunGitOrThrow($"remote set-url origin \"{remoteUrl}\"", localPath);
                else
                    RunGitOrThrow($"remote add origin \"{remoteUrl}\"", localPath);
            }
        });
        _repositoryPath = localPath;
    }

    public async Task CloneRepositoryAsync(string remoteUrl, string localPath)
    {
        if (string.IsNullOrWhiteSpace(remoteUrl))
            throw new InvalidOperationException("リモートURLを入力してください。");
        if (string.IsNullOrWhiteSpace(localPath))
            throw new InvalidOperationException("ローカルパスを入力してください。");

        await Task.Run(() =>
        {
            var parent = Path.GetDirectoryName(localPath) ?? localPath;
            if (!Directory.Exists(parent))
                Directory.CreateDirectory(parent);
            RunGitOrThrow($"clone \"{remoteUrl}\" \"{localPath}\"", parent);
        });
        _repositoryPath = localPath;
    }

    // ─── ヘルパー ──────────────────────

    private static void EnsureRemoteBareIfLocal(string remoteUrl)
    {
        if (remoteUrl.Contains("://") || remoteUrl.Contains("@")) return;
        remoteUrl = remoteUrl.Trim().Trim('"');
        if (!Directory.Exists(remoteUrl))
        {
            Directory.CreateDirectory(remoteUrl);
            var psi = new ProcessStartInfo("git", "init --bare")
            {
                WorkingDirectory = remoteUrl,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var p = Process.Start(psi);
            p?.WaitForExit(10000);
            return;
        }
        var check = new ProcessStartInfo("git", "rev-parse --git-dir")
        {
            WorkingDirectory = remoteUrl,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var cp = Process.Start(check);
        cp?.WaitForExit(5000);
        if (cp?.ExitCode != 0)
        {
            var init = new ProcessStartInfo("git", "init --bare")
            {
                WorkingDirectory = remoteUrl,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var ip = Process.Start(init);
            ip?.WaitForExit(10000);
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
        var output = proc.StandardOutput.ReadToEnd();
        var error = proc.StandardError.ReadToEnd();
        proc.WaitForExit(30000);

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
}
