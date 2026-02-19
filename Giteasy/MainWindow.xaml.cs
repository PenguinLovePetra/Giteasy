using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Giteasy.Services;
using Giteasy.ViewModels;
using Giteasy.Views.Pages;

namespace Giteasy;

public sealed partial class MainWindow : Window
{
    private readonly GitService _gitService;
    private readonly StatusViewModel _statusVm;
    private readonly BranchViewModel _branchVm;
    private readonly SyncViewModel _syncVm;
    private readonly HistoryViewModel _historyVm;
    private readonly SettingsViewModel _settingsVm;
    private readonly RepoSetupViewModel _repoSetupVm;

    public MainWindow()
    {
        InitializeComponent();

        // タイトルバーのカスタマイズ
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        // サービスとViewModelの初期化
        _gitService = new GitService();
        _statusVm = new StatusViewModel(_gitService);
        _branchVm = new BranchViewModel(_gitService);
        _syncVm = new SyncViewModel(_gitService);
        _historyVm = new HistoryViewModel(_gitService);
        _settingsVm = new SettingsViewModel(_gitService);
        _repoSetupVm = new RepoSetupViewModel(_gitService);

        // 設定変更時にステータスバーを更新
        _settingsVm.SettingsChanged += UpdateStatusBar;
        _repoSetupVm.SetupCompleted += () =>
        {
            _settingsVm.RepositoryPath = _gitService.RepositoryPath ?? "";
            _settingsVm.LoadSettings();
            UpdateStatusBar();
        };

        // ウィンドウ参照の設定
        _settingsVm.SetWindow(this);
        _repoSetupVm.SetWindow(this);

        // 設定の読み込み
        _settingsVm.LoadSettings();
        UpdateStatusBar();
    }

    private void NavView_Loaded(object sender, RoutedEventArgs e)
    {
        // 起動時に最初のアイテムを選択
        NavView.SelectedItem = NavView.MenuItems[0];
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem item) return;

        var tag = item.Tag?.ToString();
        switch (tag)
        {
            case "RepoSetup":
                var repoSetupPage = new RepoSetupPage(_repoSetupVm);
                ContentFrame.Content = repoSetupPage;
                break;
            case "Status":
                var statusPage = new StatusPage(_statusVm);
                ContentFrame.Content = statusPage;
                break;
            case "Branch":
                var branchPage = new BranchPage(_branchVm);
                ContentFrame.Content = branchPage;
                break;
            case "Sync":
                var syncPage = new SyncPage(_syncVm);
                ContentFrame.Content = syncPage;
                break;
            case "History":
                var historyPage = new HistoryPage(_historyVm);
                ContentFrame.Content = historyPage;
                break;
            case "Settings":
                var settingsPage = new SettingsPage(_settingsVm);
                ContentFrame.Content = settingsPage;
                break;
        }

        // ステータスバー更新
        UpdateStatusBar();
    }

    private void UpdateStatusBar()
    {
        if (_gitService.IsRepositorySet)
        {
            BranchStatusText.Text = $"ブランチ: {_gitService.CurrentBranchName}";
            RepoPathText.Text = _gitService.RepositoryPath ?? "";
        }
        else
        {
            BranchStatusText.Text = "ブランチ: 未設定";
            RepoPathText.Text = "設定画面からリポジトリを指定してください";
        }
    }
}
