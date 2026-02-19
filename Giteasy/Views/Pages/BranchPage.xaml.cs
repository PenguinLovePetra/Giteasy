using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Giteasy.Models;
using Giteasy.ViewModels;

namespace Giteasy.Views.Pages;

public sealed partial class BranchPage : Page
{
    private readonly BranchViewModel _vm;

    public BranchPage(BranchViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BranchListView.ItemsSource = _vm.Branches;
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        _vm.SetXamlRoot(XamlRoot);
        _vm.PropertyChanged += (s, args) =>
        {
            if (args.PropertyName == nameof(BranchViewModel.IsBusy))
                LoadingRing.IsActive = _vm.IsBusy;
            if (args.PropertyName == nameof(BranchViewModel.CurrentBranchName))
                CurrentBranchText.Text = $"現在のブランチ: {_vm.CurrentBranchName}";
        };
        await _vm.RefreshAsync();
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
        => await _vm.RefreshAsync();

    private async void CreateBranch_Click(object sender, RoutedEventArgs e)
    {
        _vm.NewBranchName = NewBranchBox.Text;
        await _vm.CreateBranchCommand.ExecuteAsync(null);
        NewBranchBox.Text = _vm.NewBranchName;
    }

    private void BranchList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var selected = BranchListView.SelectedItem as BranchInfo;
        _vm.SelectedBranch = selected;
        CheckoutBtn.IsEnabled = selected != null && !selected.IsHead;
        MergeBtn.IsEnabled = selected != null && !selected.IsHead;
        DeleteBtn.IsEnabled = selected != null && !selected.IsHead && !selected.IsRemote;
    }

    private async void Checkout_Click(object sender, RoutedEventArgs e)
        => await _vm.CheckoutCommand.ExecuteAsync(null);

    private async void Merge_Click(object sender, RoutedEventArgs e)
        => await _vm.MergeCommand.ExecuteAsync(null);

    private async void Delete_Click(object sender, RoutedEventArgs e)
        => await _vm.DeleteBranchCommand.ExecuteAsync(null);
}
