using CommunityToolkit.Mvvm.ComponentModel;
using LibGit2Sharp;

namespace Giteasy.Models;

/// <summary>
/// 変更されたファイルの情報を表すモデル。
/// </summary>
public partial class FileChange : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected;

    public string FilePath { get; }
    public FileStatus Status { get; }

    /// <summary>
    /// UIに表示する日本語ステータス文字列。
    /// </summary>
    public string StatusText => Status switch
    {
        FileStatus.NewInWorkdir => "新規 (未追跡)",
        FileStatus.ModifiedInWorkdir => "変更あり",
        FileStatus.DeletedFromWorkdir => "削除済み",
        FileStatus.RenamedInWorkdir => "名前変更",
        FileStatus.NewInIndex => "ステージ済み (新規)",
        FileStatus.ModifiedInIndex => "ステージ済み (変更)",
        FileStatus.DeletedFromIndex => "ステージ済み (削除)",
        FileStatus.RenamedInIndex => "ステージ済み (名前変更)",
        FileStatus.Conflicted => "競合あり",
        _ => Status.ToString()
    };

    /// <summary>
    /// ステータスに応じたアイコンの色を返す。
    /// </summary>
    public string StatusColor => Status switch
    {
        FileStatus.NewInWorkdir or FileStatus.NewInIndex => "#4CAF50",      // 緑
        FileStatus.ModifiedInWorkdir or FileStatus.ModifiedInIndex => "#FF9800", // オレンジ
        FileStatus.DeletedFromWorkdir or FileStatus.DeletedFromIndex => "#F44336", // 赤
        FileStatus.Conflicted => "#E91E63",   // ピンク
        _ => "#9E9E9E"                        // グレー
    };

    public FileChange(string filePath, FileStatus status)
    {
        FilePath = filePath;
        Status = status;
        _isSelected = false;
    }
}
