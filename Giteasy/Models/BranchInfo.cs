namespace Giteasy.Models;

/// <summary>
/// ブランチ情報を表すモデル。
/// </summary>
public class BranchInfo
{
    /// <summary>ブランチの表示名。</summary>
    public string Name { get; }

    /// <summary>正規名 (refs/heads/... など)。</summary>
    public string CanonicalName { get; }

    /// <summary>現在チェックアウトされているブランチかどうか。</summary>
    public bool IsHead { get; }

    /// <summary>リモートブランチかどうか。</summary>
    public bool IsRemote { get; }

    /// <summary>追跡中のリモートブランチ名（例: origin/main）。ローカルブランチのみ有効。</summary>
    public string? TrackedRemoteName { get; }

    /// <summary>リモートと同期済みかどうか（トラッキングブランチがある場合 true）。</summary>
    public bool IsSynced => !string.IsNullOrEmpty(TrackedRemoteName);

    /// <summary>UIアイコン表示用。</summary>
    public string Icon => IsHead ? "\uE8FB" : (IsRemote ? "\uE774" : "\uE8A5");

    /// <summary>同期状態の表示テキスト。</summary>
    public string SyncStatusText => IsSynced ? $"↔ {TrackedRemoteName}" : "";

    public BranchInfo(string name, string canonicalName, bool isHead, bool isRemote, string? trackedRemoteName = null)
    {
        Name = name;
        CanonicalName = canonicalName;
        IsHead = isHead;
        IsRemote = isRemote;
        TrackedRemoteName = trackedRemoteName;
    }
}
