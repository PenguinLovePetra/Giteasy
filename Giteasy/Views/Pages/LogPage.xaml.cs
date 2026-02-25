using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Giteasy.ViewModels;

namespace Giteasy.Views.Pages;

public sealed partial class LogPage : Page
{
    private readonly LogViewModel _vm;

    public LogPage(LogViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
    }

    private bool _eventsRegistered;

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        LogList.ItemsSource = _vm.LogEntries;

        if (!_eventsRegistered)
        {
            // 新しいログが追加されたらスクロール
            _vm.LogEntries.CollectionChanged += (s, args) =>
            {
                if (_vm.LogEntries.Count > 0)
                {
                    LogList.ScrollIntoView(_vm.LogEntries[_vm.LogEntries.Count - 1]);
                }
            };
            _eventsRegistered = true;
        }
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        _vm.ClearLogCommand.Execute(null);
    }
}
