using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Giteasy.Services;

namespace Giteasy.ViewModels;

public partial class LogViewModel : ObservableObject
{
    /// <summary>ログエントリへの参照。</summary>
    public ObservableCollection<string> LogEntries => GitLogService.Entries;

    [RelayCommand]
    private void ClearLog()
    {
        GitLogService.Clear();
    }
}
