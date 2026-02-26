using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Giteasy.ViewModels;
using Windows.Storage.Pickers;
using Windows.UI;

namespace Giteasy.Views.Pages;

public sealed partial class AutoClonePage : Page
{
    private readonly AutoCloneViewModel _vm;

    public AutoClonePage(AutoCloneViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        HistoryListView.ItemsSource = _vm.CloneHistory;

        // 履歴変更時にUI更新
        _vm.CloneHistory.CollectionChanged += (_, _) => UpdateHistoryVisibility();
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        _vm.SetXamlRoot(XamlRoot);
        _vm.SetDispatcherQueue(DispatcherQueue);
        UpdateHistoryVisibility();
    }

    private async void BrowseWatchDir_Click(object sender, RoutedEventArgs e)
    {
        var path = await PickFolderAsync();
        if (path != null) _vm.WatchDirectory = path;
    }

    private async void BrowseCloneDir_Click(object sender, RoutedEventArgs e)
    {
        var path = await PickFolderAsync();
        if (path != null) _vm.CloneBaseDirectory = path;
    }

    private async void CheckNow_Click(object sender, RoutedEventArgs e)
    {
        await _vm.CheckNowCommand.ExecuteAsync(null);
    }

    private void UpdateHistoryVisibility()
    {
        var hasHistory = _vm.CloneHistory.Count > 0;
        EmptyHistoryPanel.Visibility = hasHistory ? Visibility.Collapsed : Visibility.Visible;
        HistoryListView.Visibility = hasHistory ? Visibility.Visible : Visibility.Collapsed;
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
