using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Giteasy.ViewModels;
using Windows.Storage.Pickers;

namespace Giteasy.Views.Pages;

public sealed partial class SafeDirectoryPage : Page
{
    private readonly SafeDirectoryViewModel _vm;

    public SafeDirectoryPage(SafeDirectoryViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DirListView.ItemsSource = _vm.Directories;

        _vm.Directories.CollectionChanged += (_, _) => UpdateVisibility();
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        _vm.SetXamlRoot(XamlRoot);
        await _vm.RefreshCommand.ExecuteAsync(null);
        UpdateVisibility();
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await _vm.RefreshCommand.ExecuteAsync(null);
    }

    private async void AddDir_Click(object sender, RoutedEventArgs e)
    {
        await _vm.AddDirectoryCommand.ExecuteAsync(null);
    }

    private async void RemoveDir_Click(object sender, RoutedEventArgs e)
    {
        await _vm.RemoveDirectoryCommand.ExecuteAsync(null);
    }

    private async void BrowseDir_Click(object sender, RoutedEventArgs e)
    {
        var path = await PickFolderAsync();
        if (path != null) _vm.NewDirectory = path;
    }

    private void DirList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _vm.SelectedDirectory = DirListView.SelectedItem as string;
        RemoveBtn.IsEnabled = _vm.SelectedDirectory != null;
    }

    private void UpdateVisibility()
    {
        var hasItems = _vm.Directories.Count > 0;
        EmptyPanel.Visibility = hasItems ? Visibility.Collapsed : Visibility.Visible;
        DirListView.Visibility = hasItems ? Visibility.Visible : Visibility.Collapsed;
    }

    private async Task<string?> PickFolderAsync()
    {
        var window = App.MainWindow;
        if (window == null) return null;

        var picker = new FolderPicker();
        picker.SuggestedStartLocation = PickerLocationId.Desktop;
        picker.FileTypeFilter.Add("*");

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        var folder = await picker.PickSingleFolderAsync();
        return folder?.Path;
    }
}
