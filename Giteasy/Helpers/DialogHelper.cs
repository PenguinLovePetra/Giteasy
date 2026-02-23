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
    /// 詳細情報付きエラーダイアログを表示します。
    /// 概要メッセージを表示し、「詳細を表示」ボタンで完全なエラー情報を確認できます。
    /// </summary>
    public static async Task ShowErrorWithDetailsAsync(XamlRoot xamlRoot, string title,
        string summary, string details)
    {
        var contentPanel = new StackPanel { Spacing = 12 };

        contentPanel.Children.Add(new TextBlock
        {
            Text = summary,
            TextWrapping = TextWrapping.Wrap,
        });

        var expander = new Expander
        {
            Header = "詳細情報",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
        };

        var detailsScroll = new ScrollViewer
        {
            MaxHeight = 300,
            Content = new TextBlock
            {
                Text = details,
                FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                IsTextSelectionEnabled = true,
            }
        };

        expander.Content = detailsScroll;
        contentPanel.Children.Add(expander);

        var dialog = new ContentDialog
        {
            Title = title,
            Content = contentPanel,
            CloseButtonText = "OK",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = xamlRoot,
            MinWidth = 500,
        };
        await dialog.ShowAsync();
    }

    /// <summary>
    /// 例外からエラーダイアログを表示します。詳細にスタックトレースを含みます。
    /// </summary>
    public static async Task ShowExceptionAsync(XamlRoot xamlRoot, string title, Exception ex)
    {
        var summary = ex.Message;
        var details = $"例外の種類: {ex.GetType().FullName}\n\n" +
                      $"メッセージ: {ex.Message}\n\n" +
                      $"スタックトレース:\n{ex.StackTrace}";

        if (ex.InnerException != null)
        {
            details += $"\n\n--- 内部例外 ---\n" +
                       $"種類: {ex.InnerException.GetType().FullName}\n" +
                       $"メッセージ: {ex.InnerException.Message}\n" +
                       $"スタックトレース:\n{ex.InnerException.StackTrace}";
        }

        await ShowErrorWithDetailsAsync(xamlRoot, title, summary, details);
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
