using System;
using System.Collections.Generic;

namespace Giteasy.Models;

/// <summary>
/// コミット情報を表すモデル。
/// </summary>
public class CommitInfo
{
    /// <summary>コミットSHAの短縮表示 (先頭7文字)。</summary>
    public string ShortSha { get; }

    /// <summary>コミットSHAの完全版。</summary>
    public string FullSha { get; }

    /// <summary>コミットメッセージ。</summary>
    public string Message { get; }

    /// <summary>コミット作成者名。</summary>
    public string AuthorName { get; }

    /// <summary>コミット日時。</summary>
    public DateTimeOffset When { get; }

    /// <summary>親コミットのSHA一覧。マージコミットは2つ以上持つ。</summary>
    public List<string> ParentShas { get; }

    /// <summary>このコミットに紐づくRef（HEAD, ブランチ名, タグ名）。</summary>
    public List<string> Refs { get; }

    /// <summary>UI表示用のフォーマットされた日時。</summary>
    public string FormattedDate => When.LocalDateTime.ToString("yyyy/MM/dd HH:mm");

    public CommitInfo(string fullSha, string message, string authorName, DateTimeOffset when,
        List<string>? parentShas = null, List<string>? refs = null)
    {
        FullSha = fullSha;
        ShortSha = fullSha.Length >= 7 ? fullSha[..7] : fullSha;
        Message = message;
        AuthorName = authorName;
        When = when;
        ParentShas = parentShas ?? new List<string>();
        Refs = refs ?? new List<string>();
    }
}
