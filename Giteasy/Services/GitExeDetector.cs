using System;
using System.Diagnostics;
using System.IO;

namespace Giteasy.Services;

/// <summary>
/// PC 内の git.exe を検出するユーティリティ。
/// </summary>
public static class GitExeDetector
{
    private static string? _cachedPath;
    private static bool _searched;

    /// <summary>
    /// git.exe のパスを返します。見つからない場合は null。
    /// </summary>
    public static string? DetectedPath
    {
        get
        {
            if (!_searched)
            {
                _cachedPath = FindGitExe();
                _searched = true;
            }
            return _cachedPath;
        }
    }

    /// <summary>git.exe が利用可能かどうか。</summary>
    public static bool IsAvailable => DetectedPath != null;

    /// <summary>キャッシュをクリアして再検索させます。</summary>
    public static void Reset()
    {
        _searched = false;
        _cachedPath = null;
    }

    private static string? FindGitExe()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "where",
                Arguments = "git",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var proc = Process.Start(psi);
            if (proc == null) return null;

            var output = proc.StandardOutput.ReadToEnd().Trim();
            proc.WaitForExit(5000);

            if (proc.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
                return null;

            // where git は複数行返すことがある（最初の行が最優先）
            var firstLine = output.Split('\n')[0].Trim();
            return File.Exists(firstLine) ? firstLine : null;
        }
        catch
        {
            return null;
        }
    }
}
