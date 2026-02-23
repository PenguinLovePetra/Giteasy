using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.UI.Dispatching;

namespace Giteasy.Services;

/// <summary>
/// Git コマンドの実行ログを記録する静的サービス。
/// GitExeBackend と GitService の両方からログを追加します。
/// UIスレッドセーフにObservableCollectionを操作します。
/// </summary>
public static class GitLogService
{
    /// <summary>ログエントリのコレクション。UI からバインド可能。</summary>
    public static ObservableCollection<string> Entries { get; } = new();

    private static DispatcherQueue? _dispatcherQueue;
    private static readonly object _lock = new();
    private static readonly List<string> _pendingEntries = new();

    /// <summary>UIスレッドのDispatcherQueueを設定します。App起動時に呼び出してください。</summary>
    public static void Initialize(DispatcherQueue dispatcherQueue)
    {
        _dispatcherQueue = dispatcherQueue;

        // 保留中のエントリを追加
        lock (_lock)
        {
            foreach (var entry in _pendingEntries)
                Entries.Add(entry);
            _pendingEntries.Clear();

            while (Entries.Count > 1000)
                Entries.RemoveAt(0);
        }
    }

    /// <summary>ログにエントリを追加します。どのスレッドからでも安全に呼び出せます。</summary>
    public static void Log(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        var entry = $"[{timestamp}] {message}";

        if (_dispatcherQueue != null)
        {
            _dispatcherQueue.TryEnqueue(() =>
            {
                Entries.Add(entry);
                while (Entries.Count > 1000)
                    Entries.RemoveAt(0);
            });
        }
        else
        {
            // DispatcherQueue未設定の場合は保留リストに追加
            lock (_lock)
            {
                _pendingEntries.Add(entry);
                while (_pendingEntries.Count > 1000)
                    _pendingEntries.RemoveAt(0);
            }
        }
    }

    /// <summary>ログをクリアします。</summary>
    public static void Clear()
    {
        if (_dispatcherQueue != null)
        {
            _dispatcherQueue.TryEnqueue(() => Entries.Clear());
        }
        else
        {
            lock (_lock)
            {
                _pendingEntries.Clear();
            }
        }
    }
}
