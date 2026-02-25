using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Giteasy.Models;
using Giteasy.Services;
using Giteasy.ViewModels;

namespace Giteasy.Views.Pages;

public sealed partial class SettingsPage : Page
{
    private readonly SettingsViewModel _vm;

    public SettingsPage(SettingsViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
    }

    private bool _eventsRegistered;

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        _vm.SetXamlRoot(XamlRoot);

        // テーマ ComboBox 初期化
        ThemeCombo.ItemsSource = _vm.AvailableThemes;
        var currentTheme = _vm.SelectedTheme;
        for (int i = 0; i < _vm.AvailableThemes.Count; i++)
        {
            if (_vm.AvailableThemes[i].Key == currentTheme)
            {
                ThemeCombo.SelectedIndex = i;
                break;
            }
        }

        // バックエンド ComboBox 初期化
        var backendItems = new List<(string Key, string Display)>
        {
            (BackendModes.Builtin, "同梱版 (LibGit2Sharp)"),
        };
        if (_vm.IsGitExeAvailable)
            backendItems.Add((BackendModes.System, "システム版 (git.exe)"));

        BackendCombo.ItemsSource = backendItems.Select(b => b.Display).ToList();
        var backendIndex = backendItems.FindIndex(b => b.Key == _vm.SelectedBackend);
        BackendCombo.SelectedIndex = backendIndex >= 0 ? backendIndex : 0;

        // git.exe パス表示
        GitExePathText.Text = _vm.IsGitExeAvailable
            ? $"git.exe: {_vm.GitExePath}"
            : "git.exe: 見つかりません（同梱版のみ利用可）";

        // ViewModel → UI の同期
        UserNameBox.Text = _vm.UserName;
        UserEmailBox.Text = _vm.UserEmail;

        // イベントの重複登録を防止
        if (!_eventsRegistered)
        {
            _vm.PropertyChanged += (s, args) =>
            {
                if (args.PropertyName == nameof(SettingsViewModel.StatusMessage))
                    StatusText.Text = _vm.StatusMessage;
            };
            _eventsRegistered = true;
        }
    }

    private void ThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ThemeCombo.SelectedItem is ThemeOption theme)
        {
            _vm.SelectedTheme = theme.Key;
        }
    }

    private void BackendCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (BackendCombo.SelectedIndex == 0)
            _vm.SelectedBackend = BackendModes.Builtin;
        else if (BackendCombo.SelectedIndex == 1 && _vm.IsGitExeAvailable)
            _vm.SelectedBackend = BackendModes.System;
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        _vm.UserName = UserNameBox.Text;
        _vm.UserEmail = UserEmailBox.Text;
        await _vm.SaveSettingsCommand.ExecuteAsync(null);

        // テーマをランタイム適用（リフレクション不要、VMのパブリックプロパティを使用）
        var window = _vm.Window;
        if (window != null)
            App.ApplyTheme(window, _vm.SelectedTheme);
    }
}
