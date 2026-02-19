using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Giteasy.Models;
using Giteasy.ViewModels;

namespace Giteasy.Views.Pages;

public sealed partial class HistoryPage : Page
{
    private readonly HistoryViewModel _vm;

    public HistoryPage(HistoryViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        CommitListView.ItemsSource = _vm.Commits;
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        _vm.SetXamlRoot(XamlRoot);
        _vm.PropertyChanged += (s, args) =>
        {
            if (args.PropertyName == nameof(HistoryViewModel.IsBusy))
                LoadingRing.IsActive = _vm.IsBusy;
        };
        await _vm.RefreshAsync();
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
        => await _vm.RefreshAsync();

    private void CommitList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var selected = CommitListView.SelectedItem as CommitInfo;
        _vm.SelectedCommit = selected;
        RevertBtn.IsEnabled = selected != null;
        RevertHint.Visibility = selected == null ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void Revert_Click(object sender, RoutedEventArgs e)
        => await _vm.RevertCommand.ExecuteAsync(null);
}
