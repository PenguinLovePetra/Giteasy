using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Giteasy.Helpers;
using Giteasy.Models;
using Giteasy.Services;
using Microsoft.UI.Xaml;

namespace Giteasy.ViewModels;

public partial class ProjectsViewModel : ObservableObject
{
    private readonly GitService _git;
    private readonly DatabaseService _db;
    private XamlRoot? _xamlRoot;

    public ObservableCollection<ProjectInfo> Projects { get; } = new();

    [ObservableProperty]
    private ProjectInfo? _selectedProject;

    /// <summary>プロジェクト切替時に呼ばれる。</summary>
    public event Action<ProjectInfo>? ProjectOpened;

    public ProjectsViewModel(GitService git, DatabaseService db)
    {
        _git = git;
        _db = db;
    }

    public void SetXamlRoot(XamlRoot root) => _xamlRoot = root;

    public void Refresh()
    {
        Projects.Clear();
        foreach (var p in _db.GetAllProjects())
            Projects.Add(p);
    }

    [RelayCommand]
    private async Task OpenProjectAsync()
    {
        if (SelectedProject == null || _xamlRoot == null) return;

        try
        {
            _git.SetRepository(SelectedProject.LocalPath);
            _db.UpdateLastOpened(SelectedProject.LocalPath);
            ProjectOpened?.Invoke(SelectedProject);
            await DialogHelper.ShowInfoAsync(_xamlRoot, "プロジェクト切替",
                $"「{SelectedProject.Name}」を開きました。");
            Refresh();
        }
        catch (Exception ex)
        {
            await DialogHelper.ShowErrorAsync(_xamlRoot, "エラー", ex.Message);
        }
    }

    [RelayCommand]
    private async Task DeleteProjectAsync()
    {
        if (SelectedProject == null || _xamlRoot == null) return;

        var confirmed = await DialogHelper.ShowConfirmAsync(_xamlRoot, "プロジェクト削除",
            $"「{SelectedProject.Name}」を一覧から削除しますか？\n（リポジトリ自体は削除されません）");
        if (!confirmed) return;

        _db.DeleteProject(SelectedProject.Id);
        Refresh();
    }
}
