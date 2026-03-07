using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace Giteasy.Services;

/// <summary>
/// Git の safe.directory 設定を管理するサービスです。
/// git config --global --get-all safe.directory で一覧を取得し、
/// git config --global --add safe.directory で追加します。
/// </summary>
public class SafeDirectoryService
{
    private readonly string _gitExePath;

    public SafeDirectoryService(string? gitExePath = null)
    {
        _gitExePath = gitExePath ?? "git";

        // GitExeDetector から取得を試みる
        if (gitExePath == null && GitExeDetector.IsAvailable)
            _gitExePath = GitExeDetector.DetectedPath!;
    }

    /// <summary>
    /// 現在登録されている safe.directory の一覧を取得します。
    /// </summary>
    public List<string> GetSafeDirectories()
    {
        var result = RunGit("config --global --get-all safe.directory");
        if (result.ExitCode != 0)
            return new List<string>();

        return result.Output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrEmpty(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// 指定パスが safe.directory に登録されているか確認します。
    /// </summary>
    public bool IsSafeDirectory(string path)
    {
        var dirs = GetSafeDirectories();
        var normalized = NormalizePath(path);
        return dirs.Any(d => string.Equals(NormalizePath(d), normalized, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 指定パスを safe.directory に追加します。
    /// 既に登録済みの場合は何もしません。
    /// </summary>
    public void AddSafeDirectory(string path)
    {
        if (IsSafeDirectory(path))
        {
            GitLogService.Log($"[SafeDirectory] 既に登録済み: {path}");
            return;
        }

        // git config では / 区切りのパスを使用
        var gitPath = path.Replace('\\', '/');
        var result = RunGit($"config --global --add safe.directory \"{gitPath}\"");
        if (result.ExitCode == 0)
        {
            GitLogService.Log($"[SafeDirectory] 追加: {path}");
        }
        else
        {
            GitLogService.Log($"[SafeDirectory] 追加失敗: {path} — {result.Error}");
            throw new InvalidOperationException($"safe.directory への追加に失敗しました:\n{result.Error}");
        }
    }

    /// <summary>
    /// 指定パスを safe.directory から削除します。
    /// </summary>
    public void RemoveSafeDirectory(string path)
    {
        var gitPath = path.Replace('\\', '/');
        var result = RunGit($"config --global --unset safe.directory \"{gitPath}\"");
        if (result.ExitCode == 0)
        {
            GitLogService.Log($"[SafeDirectory] 削除: {path}");
        }
        else
        {
            // 値が見つからない場合もエラーになるが、問題ない
            GitLogService.Log($"[SafeDirectory] 削除試行: {path} (ExitCode: {result.ExitCode})");
        }
    }

    /// <summary>
    /// パスを正規化します（バックスラッシュ統一、末尾スラッシュ除去）。
    /// </summary>
    private static string NormalizePath(string path)
    {
        return path.Trim()
            .Replace('\\', '/')
            .TrimEnd('/');
    }

    private (int ExitCode, string Output, string Error) RunGit(string arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _gitExePath,
            Arguments = arguments,
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
        proc.WaitForExit(10000);
        return (proc.ExitCode, output, error);
    }
}
