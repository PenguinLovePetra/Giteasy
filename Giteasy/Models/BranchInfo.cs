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

    /// <summary>UIアイコン表示用。</summary>
    public string Icon => IsHead ? "\uE8FB" : (IsRemote ? "\uE774" : "\uE8A5");

    public BranchInfo(string name, string canonicalName, bool isHead, bool isRemote)
    {
        Name = name;
        CanonicalName = canonicalName;
        IsHead = isHead;
        IsRemote = isRemote;
    }
}
