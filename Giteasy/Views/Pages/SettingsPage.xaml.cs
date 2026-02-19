using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Giteasy.ViewModels;
using Windows.UI;

namespace Giteasy.Views.Pages;

public sealed partial class SettingsPage : Page
{
    private readonly SettingsViewModel _vm;

    public SettingsPage(SettingsViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        _vm.SetXamlRoot(XamlRoot);

        // ViewModel→UI の同期
        RepoPathBox.Text = _vm.RepositoryPath;
        UserNameBox.Text = _vm.UserName;
        UserEmailBox.Text = _vm.UserEmail;
        UpdateValidationUI();

        _vm.PropertyChanged += (s, args) =>
        {
            if (args.PropertyName == nameof(SettingsViewModel.RepositoryPath))
                RepoPathBox.Text = _vm.RepositoryPath;
            if (args.PropertyName == nameof(SettingsViewModel.StatusMessage))
                StatusText.Text = _vm.StatusMessage;
            if (args.PropertyName == nameof(SettingsViewModel.IsRepositoryValid))
                UpdateValidationUI();
        };
    }

    private async void Browse_Click(object sender, RoutedEventArgs e)
    {
        await _vm.BrowseRepositoryCommand.ExecuteAsync(null);
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        _vm.UserName = UserNameBox.Text;
        _vm.UserEmail = UserEmailBox.Text;
        await _vm.SaveSettingsCommand.ExecuteAsync(null);
    }

    private void UpdateValidationUI()
    {
        if (string.IsNullOrEmpty(_vm.RepositoryPath))
        {
            RepoValidIndicator.Visibility = Visibility.Collapsed;
            return;
        }

        RepoValidIndicator.Visibility = Visibility.Visible;
        if (_vm.IsRepositoryValid)
        {
            RepoValidIcon.Glyph = "\uE930";
            RepoValidIcon.Foreground = new SolidColorBrush(Color.FromArgb(255, 76, 175, 80));
            RepoValidText.Text = "✓ 有効な Git リポジトリです";
        }
        else
        {
            RepoValidIcon.Glyph = "\uEA39";
            RepoValidIcon.Foreground = new SolidColorBrush(Color.FromArgb(255, 244, 67, 54));
            RepoValidText.Text = "✗ Git リポジトリが見つかりません";
        }
    }
}
