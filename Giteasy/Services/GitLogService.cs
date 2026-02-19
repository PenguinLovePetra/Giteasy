using System;
using System.Collections.ObjectModel;

namespace Giteasy.Services;

/// <summary>
/// Git コマンドの実行ログを記録する静的サービス。
/// GitExeBackend と GitService の両方からログを追加します。
/// </summary>
public static class GitLogService
{
    /// <summary>ログエントリのコレクション。UI からバインド可能。</summary>
    public static ObservableCollection<string> Entries { get; } = new();

    private static readonly object _lock = new();

    /// <summary>ログにエントリを追加します。</summary>
    public static void Log(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        var entry = $"[{timestamp}] {message}";
        lock (_lock)
        {
            // UIスレッドから呼ばれない場合もあるので、Insertは後でDispatcherで行う
            // ここでは単純にAddする
            Entries.Add(entry);

            // 最大1000件に制限
            while (Entries.Count > 1000)
                Entries.RemoveAt(0);
        }
    }

    /// <summary>ログをクリアします。</summary>
    public static void Clear()
    {
        lock (_lock)
        {
            Entries.Clear();
        }
    }
}
