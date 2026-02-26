using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Giteasy.Models;
using Giteasy.Services;
using Giteasy.ViewModels;
using Giteasy.Views.Pages;

namespace Giteasy;

public sealed partial class MainWindow : Window
{
    private readonly GitService _gitService;
    private readonly DatabaseService _db;
    private readonly StatusViewModel _statusVm;
    private readonly BranchViewModel _branchVm;
    private readonly SyncViewModel _syncVm;
    private readonly HistoryViewModel _historyVm;
    private readonly SettingsViewModel _settingsVm;
    private readonly RepoSetupViewModel _repoSetupVm;
    private readonly ProjectsViewModel _projectsVm;
    private readonly LogViewModel _logVm;
    private readonly AutoCloneService _autoCloneService;
    private readonly AutoCloneViewModel _autoCloneVm;

    public MainWindow()
    {
        InitializeComponent();

        // タイトルバー
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        // サービス初期化
        _gitService = new GitService();
        _db = App.Database;

        // ViewModel 初期化
        _statusVm = new StatusViewModel(_gitService);
        _branchVm = new BranchViewModel(_gitService);
        _syncVm = new SyncViewModel(_gitService);
        _historyVm = new HistoryViewModel(_gitService);
        _settingsVm = new SettingsViewModel(_gitService, _db);
        _repoSetupVm = new RepoSetupViewModel(_gitService);
        _projectsVm = new ProjectsViewModel(_gitService, _db);
        _logVm = new LogViewModel();
        _autoCloneService = new AutoCloneService(_gitService, _db);
        _autoCloneVm = new AutoCloneViewModel(_autoCloneService, _db);

        // 設定変更 → ステータスバー更新
        _settingsVm.SettingsChanged += UpdateStatusBar;

        // テーマ変更 → ランタイム適用
        _settingsVm.ThemeChanged += theme => App.ApplyTheme(this, theme);

        // セットアップ完了 → プロジェクト登録 & 設定更新
        _repoSetupVm.SetupCompleted += () =>
        {
            // DB にプロジェクトを登録
            var localPath = _gitService.RepositoryPath ?? "";
            if (!string.IsNullOrEmpty(localPath))
            {
                var name = System.IO.Path.GetFileName(localPath);
                if (string.IsNullOrEmpty(name)) name = localPath;
                _db.AddProject(new ProjectInfo
                {
                    Name = name,
                    LocalPath = localPath,
                    RemoteUrl = _repoSetupVm.InitRemoteUrl ?? _repoSetupVm.CloneRemoteUrl ?? "",
                });
            }

            _settingsVm.RepositoryPath = localPath;
            _settingsVm.LoadSettings();
            UpdateStatusBar();
        };

        // プロジェクト切替 → 設定更新
        _projectsVm.ProjectOpened += project =>
        {
            _settingsVm.RepositoryPath = project.LocalPath;
            _settingsVm.LoadSettings();
            UpdateStatusBar();
        };

        // ウィンドウ参照
        _settingsVm.SetWindow(this);
        _repoSetupVm.SetWindow(this);
        _autoCloneVm.SetWindow(this);

        // AutoClone でプロジェクトが追加されたらリフレッシュ
        _autoCloneVm.ProjectListChanged += () => _projectsVm.Refresh();

        // 設定読み込み & テーマ初期適用
        _settingsVm.LoadSettings();
        App.ApplyTheme(this, _settingsVm.SelectedTheme);
        UpdateStatusBar();
    }

    private async void NavView_Loaded(object sender, RoutedEventArgs e)
    {
        NavView.SelectedItem = NavView.MenuItems[0];

        // 起動時に1度だけ AutoClone のチェックを走らせる
        try
        {
            // UIツリーの準備が整ってから実行する
            if (_autoCloneVm.CheckNowCommand.CanExecute(null))
            {
                await _autoCloneVm.CheckNowCommand.ExecuteAsync(null);
            }
        }
        catch { /* 初回スキャンエラーは無視する */ }
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem item) return;

        var tag = item.Tag?.ToString();
        switch (tag)
        {
            case "Projects":
                ContentFrame.Content = new ProjectsPage(_projectsVm);
                break;
            case "RepoSetup":
                ContentFrame.Content = new RepoSetupPage(_repoSetupVm);
                break;
            case "AutoClone":
                ContentFrame.Content = new AutoClonePage(_autoCloneVm);
                break;
            case "Status":
                ContentFrame.Content = new StatusPage(_statusVm);
                break;
            case "Branch":
                ContentFrame.Content = new BranchPage(_branchVm);
                break;
            case "Sync":
                ContentFrame.Content = new SyncPage(_syncVm);
                break;
            case "History":
                ContentFrame.Content = new HistoryPage(_historyVm);
                break;
            case "Settings":
                ContentFrame.Content = new SettingsPage(_settingsVm);
                break;
            case "Log":
                ContentFrame.Content = new LogPage(_logVm);
                break;
        }

        UpdateStatusBar();
    }

    private void UpdateStatusBar()
    {
        try
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
        catch
        {
            BranchStatusText.Text = "ブランチ: 不明";
            RepoPathText.Text = "";
        }
    }
}
