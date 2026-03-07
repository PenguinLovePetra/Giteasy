using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Giteasy.Helpers;
using Giteasy.Models;
using Giteasy.Services;
using Giteasy.ViewModels;
using Windows.UI;

namespace Giteasy.Views.Pages;

public sealed partial class RepositoryPage : Page
{
    private readonly SyncViewModel _syncVm;
    private readonly BranchViewModel _branchVm;
    private readonly HistoryViewModel _historyVm;
    private readonly GitService _git;

    // Graph描画定数
    private static readonly Color[] LaneColors = new[]
    {
        Color.FromArgb(255, 97, 175, 239),
        Color.FromArgb(255, 152, 195, 121),
        Color.FromArgb(255, 224, 108, 117),
        Color.FromArgb(255, 229, 192, 123),
        Color.FromArgb(255, 198, 120, 221),
        Color.FromArgb(255, 86, 182, 194),
        Color.FromArgb(255, 209, 154, 102),
        Color.FromArgb(255, 171, 178, 191),
    };
    private static readonly Color HeadColor = Color.FromArgb(255, 97, 175, 239);
    private static readonly Color BranchColor = Color.FromArgb(255, 152, 195, 121);
    private static readonly Color TagColor = Color.FromArgb(255, 229, 192, 123);
    private const double LaneWidth = 14.0;
    private const double DotRadius = 4.0;
    private const double LineThickness = 2.0;
    private const double RowHeight = 40.0;
    private const double GraphPaddingLeft = 8.0;

    public RepositoryPage(SyncViewModel syncVm, BranchViewModel branchVm,
                           HistoryViewModel historyVm, GitService git)
    {
        InitializeComponent();
        _syncVm = syncVm;
        _branchVm = branchVm;
        _historyVm = historyVm;
        _git = git;

        BranchListView.ItemsSource = _branchVm.Branches;
        CommitListView.ItemsSource = _historyVm.GraphNodes;
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        _syncVm.SetXamlRoot(XamlRoot);
        _branchVm.SetXamlRoot(XamlRoot);
        _historyVm.SetXamlRoot(XamlRoot);

        _historyVm.PropertyChanged += (s, args) =>
        {
            if (args.PropertyName == nameof(HistoryViewModel.IsBusy))
                LoadingRing.IsActive = _historyVm.IsBusy;
        };

        await RefreshAllAsync();
    }

    // ─── 更新 ───────────────────────────────

    private async System.Threading.Tasks.Task RefreshAllAsync()
    {
        _syncVm.Refresh();
        UpdateBranchDisplay();
        await _branchVm.RefreshCommand.ExecuteAsync(null);
        await _historyVm.RefreshCommand.ExecuteAsync(null);
    }

    private void UpdateBranchDisplay()
    {
        CurrentBranchText.Text = _git.IsRepositorySet ? _git.CurrentBranchName : "未設定";
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
        => await RefreshAllAsync();

    // ─── 同期操作 ────────────────────────────

    private async void Fetch_Click(object sender, RoutedEventArgs e)
    {
        SyncStatusText.Text = "フェッチ中...";
        await _syncVm.FetchCommand.ExecuteAsync(null);
        SyncStatusText.Text = "";
        await _historyVm.RefreshCommand.ExecuteAsync(null);
    }

    private async void Pull_Click(object sender, RoutedEventArgs e)
    {
        SyncStatusText.Text = "Pull 中...";
        await _syncVm.PullCommand.ExecuteAsync(null);
        SyncStatusText.Text = "";
        await RefreshAllAsync();
    }

    private async void Push_Click(object sender, RoutedEventArgs e)
    {
        SyncStatusText.Text = "Push 中...";
        await _syncVm.PushCommand.ExecuteAsync(null);
        SyncStatusText.Text = "";
        await _historyVm.RefreshCommand.ExecuteAsync(null);
    }

    // ─── ブランチ操作 ────────────────────────

    private void BranchList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var selected = BranchListView.SelectedItem as BranchInfo;
        _branchVm.SelectedBranch = selected;
        var canOperate = selected != null && !selected.IsHead;
        CheckoutBtn.IsEnabled = canOperate;
        MergeBtn.IsEnabled = canOperate;
        DeleteBranchBtn.IsEnabled = canOperate && !selected!.IsRemote;
    }

    private async void Checkout_Click(object sender, RoutedEventArgs e)
    {
        await _branchVm.CheckoutCommand.ExecuteAsync(null);
        await RefreshAllAsync();
    }

    private async void Merge_Click(object sender, RoutedEventArgs e)
    {
        await _branchVm.MergeCommand.ExecuteAsync(null);
        await RefreshAllAsync();
    }

    private async void DeleteBranch_Click(object sender, RoutedEventArgs e)
    {
        await _branchVm.DeleteBranchCommand.ExecuteAsync(null);
        await _branchVm.RefreshCommand.ExecuteAsync(null);
    }

    private async void CreateBranch_Click(object sender, RoutedEventArgs e)
    {
        var branchName = NewBranchBox.Text.Trim();
        if (string.IsNullOrEmpty(branchName))
        {
            await DialogHelper.ShowErrorAsync(XamlRoot, "入力エラー", "ブランチ名を入力してください。");
            return;
        }

        try
        {
            var selectedCommit = (CommitListView.SelectedItem as GraphNode)?.Commit;
            if (selectedCommit != null)
            {
                await System.Threading.Tasks.Task.Run(() =>
                    _git.CreateBranchFromCommit(branchName, selectedCommit.FullSha));
                await DialogHelper.ShowInfoAsync(XamlRoot, "作成完了",
                    $"ブランチ '{branchName}' を {selectedCommit.ShortSha} から作成しました。");
            }
            else
            {
                await System.Threading.Tasks.Task.Run(() => _git.CreateBranch(branchName));
                await DialogHelper.ShowInfoAsync(XamlRoot, "作成完了",
                    $"ブランチ '{branchName}' を作成しました。");
            }
            NewBranchBox.Text = "";
            await _branchVm.RefreshCommand.ExecuteAsync(null);
        }
        catch (Exception ex)
        {
            GitLogService.Log($"[ブランチ作成エラー] {ex.Message}");
            await DialogHelper.ShowExceptionAsync(XamlRoot, "ブランチ作成エラー", ex);
        }
    }

    private async void CreateBranchFromCommit_Click(object sender, RoutedEventArgs e)
    {
        var selected = (CommitListView.SelectedItem as GraphNode)?.Commit;
        if (selected == null) return;

        var branchName = NewBranchBox.Text.Trim();
        if (string.IsNullOrEmpty(branchName))
        {
            // ブランチ名入力を促すダイアログ
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = "ブランチ名を入力",
                Content = new TextBox
                {
                    PlaceholderText = "ブランチ名",
                    Name = "BranchNameInput"
                },
                PrimaryButtonText = "作成",
                CloseButtonText = "キャンセル",
                DefaultButton = ContentDialogButton.Primary,
            };

            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
            branchName = ((TextBox)dialog.Content).Text.Trim();
            if (string.IsNullOrEmpty(branchName)) return;
        }

        try
        {
            await System.Threading.Tasks.Task.Run(() =>
                _git.CreateBranchFromCommit(branchName, selected.FullSha));
            await DialogHelper.ShowInfoAsync(XamlRoot, "作成完了",
                $"ブランチ '{branchName}' を {selected.ShortSha} から作成しました。");
            NewBranchBox.Text = "";
            await _branchVm.RefreshCommand.ExecuteAsync(null);
        }
        catch (Exception ex)
        {
            GitLogService.Log($"[ブランチ作成エラー] {ex.Message}");
            await DialogHelper.ShowExceptionAsync(XamlRoot, "ブランチ作成エラー", ex);
        }
    }

    // ─── 履歴操作 ────────────────────────────

    private void CommitList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var selected = (CommitListView.SelectedItem as GraphNode)?.Commit;
        _historyVm.SelectedCommit = selected;
        var hasSelection = selected != null;
        RevertBtn.IsEnabled = hasSelection;
        CreateBranchFromCommitBtn.IsEnabled = hasSelection;
        SelectedCommitText.Text = hasSelection
            ? $"{selected!.ShortSha} — {selected.Message}"
            : "コミットを選択してください";
    }

    private async void Revert_Click(object sender, RoutedEventArgs e)
    {
        await _historyVm.RevertCommand.ExecuteAsync(null);
        await _historyVm.RefreshCommand.ExecuteAsync(null);
    }

    // ─── グラフ描画 ─────────────────────────

    private void CommitList_ContainerContentChanging(
        ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.InRecycleQueue) return;
        if (args.Item is not GraphNode) return;
        args.RegisterUpdateCallback(DrawGraphCallback);
    }

    private void DrawGraphCallback(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.Item is not GraphNode node) return;

        try
        {
            var container = args.ItemContainer;
            var canvas = FindChild<Canvas>(container, "GraphCanvas");
            var refLabels = FindChild<ItemsControl>(container, "RefLabels");
            if (canvas == null) return;

            canvas.Children.Clear();

            var graphWidth = Math.Max(3, node.MaxLaneCount + 1) * LaneWidth + GraphPaddingLeft * 2;
            if (canvas.Parent is Grid parentGrid && parentGrid.ColumnDefinitions.Count > 0)
                parentGrid.ColumnDefinitions[0].Width = new GridLength(graphWidth);
            canvas.Width = graphWidth;
            canvas.Height = RowHeight;

            var centerY = RowHeight / 2.0;
            var commitX = GetLaneX(node.Lane);

            // パススルーレーン
            foreach (var activeLane in node.ActiveLanes)
            {
                var x = GetLaneX(activeLane);
                DrawLine(canvas, x, 0, x, RowHeight, GetLaneColor(activeLane), LineThickness);
            }

            // 上半分接続: 既存レーンで待たれていたコミットのみ縦線を描画
            if (node.HasParentAbove)
            {
                DrawLine(canvas, commitX, 0, commitX, centerY, GetLaneColor(node.Lane), LineThickness);
            }

            // 合流線（マージ時の別レーンからの接続）
            foreach (var incoming in node.IncomingEdges)
            {
                DrawBezierEdge(canvas, GetLaneX(incoming.FromLane), 0,
                               GetLaneX(node.Lane), centerY, GetLaneColor(incoming.ColorIndex));
            }

            // 下方向エッジ
            foreach (var edge in node.Edges)
            {
                var fromX = commitX;
                var toX = GetLaneX(edge.ToLane);
                var color = GetLaneColor(edge.ColorIndex);
                if (edge.FromLane == edge.ToLane)
                    DrawLine(canvas, fromX, centerY, toX, RowHeight, color, LineThickness);
                else
                    DrawBezierEdge(canvas, fromX, centerY, toX, RowHeight, color);
            }

            // コミットドット
            DrawCommitDot(canvas, commitX, centerY, node);

            // Refラベル
            if (refLabels != null)
            {
                var labels = new List<FrameworkElement>();
                foreach (var refName in node.Commit.Refs)
                    labels.Add(CreateRefLabel(refName));
                refLabels.ItemsSource = labels;
            }
        }
        catch (Exception ex)
        {
            GitLogService.Log($"[グラフ描画エラー] {ex.Message}");
        }
    }

    // ─── 描画ヘルパー ───────────────────────

    private static double GetLaneX(int lane)
        => lane * LaneWidth + LaneWidth / 2.0 + GraphPaddingLeft;

    private static Color GetLaneColor(int index)
        => LaneColors[Math.Abs(index) % LaneColors.Length];

    private static void DrawLine(Canvas canvas, double x1, double y1, double x2, double y2,
        Color color, double thickness)
    {
        canvas.Children.Add(new Line
        {
            X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
            Stroke = new SolidColorBrush(color),
            StrokeThickness = thickness,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
        });
    }

    private static void DrawBezierEdge(Canvas canvas, double fromX, double fromY,
        double toX, double toY, Color color)
    {
        var pathFigure = new Microsoft.UI.Xaml.Media.PathFigure
        {
            StartPoint = new Windows.Foundation.Point(fromX, fromY),
        };
        var controlOffset = Math.Abs(toY - fromY) * 0.6;
        pathFigure.Segments.Add(new Microsoft.UI.Xaml.Media.BezierSegment
        {
            Point1 = new Windows.Foundation.Point(fromX, fromY + controlOffset),
            Point2 = new Windows.Foundation.Point(toX, toY - controlOffset),
            Point3 = new Windows.Foundation.Point(toX, toY),
        });
        var pathGeometry = new Microsoft.UI.Xaml.Media.PathGeometry();
        pathGeometry.Figures.Add(pathFigure);
        canvas.Children.Add(new Path
        {
            Data = pathGeometry,
            Stroke = new SolidColorBrush(color),
            StrokeThickness = LineThickness,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
        });
    }

    private static void DrawCommitDot(Canvas canvas, double x, double y, GraphNode node)
    {
        var color = GetLaneColor(node.Lane);
        var isMerge = node.Commit.ParentShas.Count > 1;
        var radius = isMerge ? DotRadius + 1.0 : DotRadius;
        var dot = new Ellipse
        {
            Width = radius * 2, Height = radius * 2,
            Fill = new SolidColorBrush(color),
        };
        Canvas.SetLeft(dot, x - radius);
        Canvas.SetTop(dot, y - radius);
        canvas.Children.Add(dot);
        if (isMerge)
        {
            var innerRadius = radius * 0.45;
            var bgBrush = Application.Current.Resources.TryGetValue(
                "LayerFillColorDefaultBrush", out var res) && res is SolidColorBrush brush
                ? brush : new SolidColorBrush(Color.FromArgb(255, 30, 30, 30));
            var innerDot = new Ellipse
            {
                Width = innerRadius * 2, Height = innerRadius * 2,
                Fill = bgBrush,
            };
            Canvas.SetLeft(innerDot, x - innerRadius);
            Canvas.SetTop(innerDot, y - innerRadius);
            canvas.Children.Add(innerDot);
        }
    }

    private static Border CreateRefLabel(string refName)
    {
        Color bgColor;
        string displayName;
        string iconGlyph;
        if (refName == "HEAD")
        {
            bgColor = HeadColor; displayName = "HEAD"; iconGlyph = "\uE72A";
        }
        else if (refName.StartsWith("tag:"))
        {
            bgColor = TagColor;
            displayName = refName.StartsWith("tag: ") ? refName["tag: ".Length..] : refName[4..];
            iconGlyph = "\uE8EC";
        }
        else
        {
            bgColor = BranchColor; displayName = refName; iconGlyph = "\uE8AD";
        }
        var border = new Border
        {
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 2, 6, 2),
            Margin = new Thickness(0, 0, 4, 0),
            Background = new SolidColorBrush(Color.FromArgb(40, bgColor.R, bgColor.G, bgColor.B)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(120, bgColor.R, bgColor.G, bgColor.B)),
            BorderThickness = new Thickness(1),
        };
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 3 };
        panel.Children.Add(new FontIcon { Glyph = iconGlyph, FontSize = 9, Foreground = new SolidColorBrush(bgColor) });
        panel.Children.Add(new TextBlock
        {
            Text = displayName, FontSize = 10,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = new SolidColorBrush(bgColor),
            VerticalAlignment = VerticalAlignment.Center,
        });
        border.Child = panel;
        return border;
    }

    private static T? FindChild<T>(DependencyObject parent, string name) where T : FrameworkElement
    {
        if (parent == null) return null;
        var childCount = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < childCount; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T found && found.Name == name) return found;
            var result = FindChild<T>(child, name);
            if (result != null) return result;
        }
        return null;
    }
}
