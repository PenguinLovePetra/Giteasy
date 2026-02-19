using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Giteasy.Helpers;

/// <summary>
/// ContentDialog を使ったダイアログ表示ユーティリティ。
/// </summary>
public static class DialogHelper
{
    /// <summary>
    /// エラーダイアログを表示します。
    /// </summary>
    public static async Task ShowErrorAsync(XamlRoot xamlRoot, string title, string message)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = "OK",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = xamlRoot
        };
        await dialog.ShowAsync();
    }

    /// <summary>
    /// 情報ダイアログを表示します。
    /// </summary>
    public static async Task ShowInfoAsync(XamlRoot xamlRoot, string title, string message)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = "OK",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = xamlRoot
        };
        await dialog.ShowAsync();
    }

    /// <summary>
    /// 確認ダイアログを表示し、ユーザーの選択を返します。
    /// </summary>
    /// <returns>Primary ボタンが押された場合 true。</returns>
    public static async Task<bool> ShowConfirmAsync(XamlRoot xamlRoot, string title, string message,
        string primaryText = "はい", string cancelText = "キャンセル")
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            PrimaryButtonText = primaryText,
            CloseButtonText = cancelText,
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = xamlRoot
        };
        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }
}
