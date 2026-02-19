namespace Giteasy.Services;

/// <summary>
/// Gitバックエンドモードの定数。
/// </summary>
public static class BackendModes
{
    /// <summary>同梱版 (LibGit2Sharp)</summary>
    public const string Builtin = "builtin";

    /// <summary>システム版 (git.exe)</summary>
    public const string System = "system";
}
