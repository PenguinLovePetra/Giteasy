using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Giteasy.Models;
using Giteasy.ViewModels;

namespace Giteasy.Views.Pages;

public sealed partial class ProjectsPage : Page
{
    private readonly ProjectsViewModel _vm;

    public ProjectsPage(ProjectsViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        ProjectListView.ItemsSource = _vm.Projects;
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        _vm.SetXamlRoot(XamlRoot);
        _vm.Refresh();
        UpdateEmptyState();
    }

    private void ProjectList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var selected = ProjectListView.SelectedItem as ProjectInfo;
        _vm.SelectedProject = selected;
        OpenBtn.IsEnabled = selected != null;
        DeleteBtn.IsEnabled = selected != null;
    }

    private async void Open_Click(object sender, RoutedEventArgs e)
    {
        await _vm.OpenProjectCommand.ExecuteAsync(null);
        UpdateEmptyState();
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        await _vm.DeleteProjectCommand.ExecuteAsync(null);
        UpdateEmptyState();
    }

    private void UpdateEmptyState()
    {
        EmptyPanel.Visibility = _vm.Projects.Count == 0
            ? Visibility.Visible : Visibility.Collapsed;
        ProjectListView.Visibility = _vm.Projects.Count > 0
            ? Visibility.Visible : Visibility.Collapsed;
    }

    private void GoToSetup_Click(object sender, RoutedEventArgs e)
    {
        // MainWindow の NavView を取得してセットアップタブに遷移
        if (App.MainWindow?.Content is Grid rootGrid)
        {
            var navView = FindNavView(rootGrid);
            if (navView != null && navView.MenuItems.Count > 1)
            {
                navView.SelectedItem = navView.MenuItems[1]; // Tag="RepoSetup"
            }
        }
    }

    private static NavigationView? FindNavView(DependencyObject parent)
    {
        var count = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < count; i++)
        {
            var child = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is NavigationView nav) return nav;
            var result = FindNavView(child);
            if (result != null) return result;
        }
        return null;
    }
}
