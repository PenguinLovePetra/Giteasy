using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Giteasy.ViewModels;

namespace Giteasy.Views.Pages;

public sealed partial class StatusPage : Page
{
    private readonly StatusViewModel _vm;

    public StatusPage(StatusViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        FileListView.ItemsSource = _vm.ChangedFiles;
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        _vm.SetXamlRoot(XamlRoot);
        _vm.PropertyChanged += (s, args) =>
        {
            if (args.PropertyName == nameof(StatusViewModel.IsBusy))
            {
                LoadingRing.IsActive = _vm.IsBusy;
            }
            if (args.PropertyName == nameof(StatusViewModel.StatusMessage))
            {
                StatusText.Text = _vm.StatusMessage;
            }
        };
        _vm.ChangedFiles.CollectionChanged += (s, args) =>
        {
            EmptyState.Visibility = _vm.ChangedFiles.Count == 0 && !_vm.IsBusy
                ? Visibility.Visible : Visibility.Collapsed;
        };
        await _vm.RefreshAsync();
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
        => await _vm.RefreshAsync();

    private void SelectAll_Click(object sender, RoutedEventArgs e)
        => _vm.SelectAllCommand.Execute(null);

    private void DeselectAll_Click(object sender, RoutedEventArgs e)
        => _vm.DeselectAllCommand.Execute(null);

    private async void Commit_Click(object sender, RoutedEventArgs e)
    {
        _vm.CommitMessage = CommitMessageBox.Text;
        await _vm.CommitCommand.ExecuteAsync(null);
        CommitMessageBox.Text = _vm.CommitMessage; // cleared after successful commit
    }

    private async void DiscardChanges_Click(object sender, RoutedEventArgs e)
        => await _vm.DiscardChangesCommand.ExecuteAsync(null);
}
