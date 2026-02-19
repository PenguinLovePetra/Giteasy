using System.Collections.Generic;

namespace Giteasy.Models;

/// <summary>
/// テーマのオプション定義。
/// </summary>
public class ThemeOption
{
    public string Key { get; set; } = "";
    public string DisplayName { get; set; } = "";
}

/// <summary>
/// アプリで利用可能なテーマの定数と一覧。
/// </summary>
public static class AppThemes
{
    public const string Light = "Light";
    public const string Dark = "Dark";
    public const string Glass = "Glass";

    public static List<ThemeOption> AvailableThemes =>
    [
        new() { Key = Light, DisplayName = "ライトテーマ" },
        new() { Key = Dark, DisplayName = "ダークテーマ" },
        new() { Key = Glass, DisplayName = "ガラステーマ" },
    ];
}
