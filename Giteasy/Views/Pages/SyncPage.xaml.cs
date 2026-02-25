using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Giteasy.ViewModels;

namespace Giteasy.Views.Pages;

public sealed partial class SyncPage : Page
{
    private readonly SyncViewModel _vm;

    public SyncPage(SyncViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
    }

    private bool _eventsRegistered;

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        _vm.SetXamlRoot(XamlRoot);
        if (!_eventsRegistered)
        {
            _vm.PropertyChanged += (s, args) =>
            {
                if (args.PropertyName == nameof(SyncViewModel.IsBusy))
                {
                    SyncProgress.IsIndeterminate = _vm.IsBusy;
                    SyncProgress.Visibility = _vm.IsBusy ? Visibility.Visible : Visibility.Collapsed;
                }
                if (args.PropertyName == nameof(SyncViewModel.StatusMessage))
                {
                    StatusText.Text = _vm.StatusMessage;
                }
            };
            _eventsRegistered = true;
        }
        _vm.Refresh();
    }

    private async void Pull_Click(object sender, RoutedEventArgs e)
        => await _vm.PullCommand.ExecuteAsync(null);

    private async void Push_Click(object sender, RoutedEventArgs e)
        => await _vm.PushCommand.ExecuteAsync(null);

    private async void Fetch_Click(object sender, RoutedEventArgs e)
        => await _vm.FetchCommand.ExecuteAsync(null);
}
