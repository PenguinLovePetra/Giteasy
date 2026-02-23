using System;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Giteasy.Helpers;
using Giteasy.Models;
using Giteasy.Services;

namespace Giteasy;

public partial class App : Application
{
    private Window? _window;

    public static DatabaseService Database { get; private set; } = null!;

    /// <summary>メインウィンドウへの参照（グローバルエラーハンドリング用）。</summary>
    public static Window? MainWindow { get; private set; }

    public App()
    {
        InitializeComponent();

        // グローバル未処理例外ハンドラー — アプリのクラッシュを防止
        UnhandledException += App_UnhandledException;
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Database = new DatabaseService();

        _window = new MainWindow();
        MainWindow = _window;
        _window.Activate();

        // GitLogServiceにUIスレッドのDispatcherQueueを設定
        GitLogService.Initialize(DispatcherQueue.GetForCurrentThread());

        // ウィンドウ作成後にテーマを適用
        var savedTheme = Database.GetSetting("theme") ?? AppThemes.Light;
        ApplyTheme(_window, savedTheme);
    }

    /// <summary>ランタイムでテーマを切り替えます。</summary>
    public static void ApplyTheme(Window window, string themeKey)
    {
        if (window.Content is FrameworkElement root)
        {
            switch (themeKey)
            {
                case AppThemes.Dark:
                    root.RequestedTheme = ElementTheme.Dark;
                    window.SystemBackdrop = new MicaBackdrop();
                    break;

                case AppThemes.Glass:
                    root.RequestedTheme = ElementTheme.Dark;
                    window.SystemBackdrop = new DesktopAcrylicBackdrop();
                    break;

                default: // Light
                    root.RequestedTheme = ElementTheme.Light;
                    window.SystemBackdrop = new MicaBackdrop();
                    break;
            }
        }
    }

    /// <summary>
    /// アプリ全体の未処理例外をキャッチし、ポップアップで表示します。
    /// アプリのクラッシュを防止し、ユーザーにエラー情報を提示します。
    /// </summary>
    private async void App_UnhandledException(object sender,
        Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        // 例外を処理済みとしてマークし、アプリのクラッシュを防止
        e.Handled = true;

        try
        {
            GitLogService.Log($"[未処理例外] {e.Exception?.GetType().Name}: {e.Exception?.Message}");

            var window = MainWindow;
            if (window?.Content is FrameworkElement root && root.XamlRoot != null)
            {
                await DialogHelper.ShowExceptionAsync(
                    root.XamlRoot,
                    "予期しないエラーが発生しました",
                    e.Exception ?? new Exception("不明なエラー"));
            }
        }
        catch
        {
            // エラーダイアログ表示中のエラーは無視（無限ループ防止）
        }
    }
}
