using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Giteasy.Services;
using Giteasy.Models;

namespace Giteasy;

public partial class App : Application
{
    private Window? _window;

    public static DatabaseService Database { get; private set; } = null!;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        Database = new DatabaseService();

        _window = new MainWindow();
        _window.Activate();

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
}
