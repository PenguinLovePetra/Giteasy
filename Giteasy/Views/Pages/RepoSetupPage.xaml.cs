using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Giteasy.ViewModels;

namespace Giteasy.Views.Pages;

public sealed partial class RepoSetupPage : Page
{
    private readonly RepoSetupViewModel _vm;

    public RepoSetupPage(RepoSetupViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        _vm.SetXamlRoot(XamlRoot);
        _vm.PropertyChanged += (s, args) =>
        {
            if (args.PropertyName == nameof(RepoSetupViewModel.IsBusy))
            {
                ProgressIndicator.IsIndeterminate = _vm.IsBusy;
                ProgressIndicator.Visibility = _vm.IsBusy ? Visibility.Visible : Visibility.Collapsed;
            }
            if (args.PropertyName == nameof(RepoSetupViewModel.StatusMessage))
            {
                StatusText.Text = _vm.StatusMessage;
            }
        };
    }

    private async void BrowseInitPath_Click(object sender, RoutedEventArgs e)
    {
        await _vm.BrowseInitPathCommand.ExecuteAsync(null);
        InitLocalPathBox.Text = _vm.InitLocalPath;
    }

    private async void BrowseClonePath_Click(object sender, RoutedEventArgs e)
    {
        await _vm.BrowseClonePathCommand.ExecuteAsync(null);
        CloneLocalPathBox.Text = _vm.CloneLocalPath;
    }

    private async void InitRepo_Click(object sender, RoutedEventArgs e)
    {
        _vm.InitLocalPath = InitLocalPathBox.Text;
        _vm.InitRemoteUrl = InitRemoteUrlBox.Text;
        await _vm.InitRepositoryCommand.ExecuteAsync(null);
    }

    private async void CloneRepo_Click(object sender, RoutedEventArgs e)
    {
        _vm.CloneRemoteUrl = CloneRemoteUrlBox.Text;
        _vm.CloneLocalPath = CloneLocalPathBox.Text;
        await _vm.CloneRepositoryCommand.ExecuteAsync(null);
    }
}
