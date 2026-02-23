using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Giteasy.Models;
using Giteasy.ViewModels;
using Windows.UI;

namespace Giteasy.Views.Pages;

public sealed partial class HistoryPage : Page
{
    private readonly HistoryViewModel _vm;

    // モダンなカラーパレット（VSCode Git Graph風）
    private static readonly Color[] LaneColors = new[]
    {
        Color.FromArgb(255, 97, 175, 239),   // 青
        Color.FromArgb(255, 152, 195, 121),   // 緑
        Color.FromArgb(255, 224, 108, 117),   // 赤
        Color.FromArgb(255, 229, 192, 123),   // 黄
        Color.FromArgb(255, 198, 120, 221),   // 紫
        Color.FromArgb(255, 86, 182, 194),    // シアン
        Color.FromArgb(255, 209, 154, 102),   // オレンジ
        Color.FromArgb(255, 171, 178, 191),   // グレー
    };

    // Refラベルの背景色
    private static readonly Color HeadColor = Color.FromArgb(255, 97, 175, 239);
    private static readonly Color BranchColor = Color.FromArgb(255, 152, 195, 121);
    private static readonly Color TagColor = Color.FromArgb(255, 229, 192, 123);

    private const double LaneWidth = 16.0;
    private const double DotRadius = 4.5;
    private const double LineThickness = 2.0;
    private const double RowHeight = 42.0;

    public HistoryPage(HistoryViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        CommitListView.ItemsSource = _vm.GraphNodes;
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        _vm.SetXamlRoot(XamlRoot);
        _vm.PropertyChanged += (s, args) =>
        {
            if (args.PropertyName == nameof(HistoryViewModel.IsBusy))
                LoadingRing.IsActive = _vm.IsBusy;
        };
        await _vm.RefreshAsync();
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
        => await _vm.RefreshAsync();

    private void CommitList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var selected = (CommitListView.SelectedItem as GraphNode)?.Commit;
        _vm.SelectedCommit = selected;
        RevertBtn.IsEnabled = selected != null;
        RevertHint.Visibility = selected == null ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void Revert_Click(object sender, RoutedEventArgs e)
        => await _vm.RevertCommand.ExecuteAsync(null);

    /// <summary>
    /// ListView アイテムが表示される際にグラフの描画とRefラベルの生成を行います。
    /// </summary>
    private void CommitList_ContainerContentChanging(
        ListViewBase sender,
        ContainerContentChangingEventArgs args)
    {
        if (args.InRecycleQueue) return;
        if (args.Item is not GraphNode node) return;

        // VisualTree から Canvas と RefLabels を探す
        args.Handled = true;
        args.RegisterUpdateCallback(DrawGraphCallback);
    }

    private void DrawGraphCallback(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.Item is not GraphNode node) return;

        var container = args.ItemContainer;
        var canvas = FindChild<Canvas>(container, "GraphCanvas");
        var refLabels = FindChild<ItemsControl>(container, "RefLabels");

        if (canvas == null) return;

        // Canvas をクリアして再描画
        canvas.Children.Clear();

        // グラフ列の幅を動的に調整
        var graphWidth = Math.Max(3, node.MaxLaneCount) * LaneWidth + 12;
        // Canvas の親 Grid の最初の ColumnDefinition の幅を更新
        if (canvas.Parent is Grid parentGrid && parentGrid.ColumnDefinitions.Count > 0)
            parentGrid.ColumnDefinitions[0].Width = new GridLength(graphWidth);
        canvas.Width = graphWidth;
        canvas.Height = RowHeight;

        var centerY = RowHeight / 2.0;

        // 1. パススルーレーンの縦線を描画
        foreach (var activeLane in node.ActiveLanes)
        {
            var x = GetLaneX(activeLane);
            var color = GetLaneColor(activeLane);
            DrawLine(canvas, x, 0, x, RowHeight, color, LineThickness);
        }

        // 2. エッジ（接続線）を描画
        foreach (var edge in node.Edges)
        {
            var fromX = GetLaneX(edge.FromLane);
            var toX = GetLaneX(edge.ToLane);
            var color = GetLaneColor(edge.ColorIndex);

            if (edge.FromLane == edge.ToLane)
            {
                // 同じレーン → 直線
                DrawLine(canvas, fromX, centerY, toX, RowHeight, color, LineThickness);
            }
            else
            {
                // 異なるレーン → ベジェ曲線で滑らかに接続
                DrawBezierEdge(canvas, fromX, centerY, toX, RowHeight, color);
            }

            // コミットドットからの上方向の縦線も描画
            if (edge.FromLane == node.Lane)
            {
                // 上方向の線（前のコミットからの接続用は上から中央まで）
                // → これはパススルーで処理されるか、最初のコミットなので不要
            }
        }

        // 第1親がある場合、上方向の線を描画（前のコミットへの接続）
        if (node.Commit.ParentShas.Count > 0 || node.ActiveLanes.Contains(node.Lane))
        {
            // このレーンに上からの線が来ている場合
            var x = GetLaneX(node.Lane);
            var color = GetLaneColor(node.Lane);
            DrawLine(canvas, x, 0, x, centerY, color, LineThickness);
        }

        // 3. コミットドットを描画（最前面）
        DrawCommitDot(canvas, GetLaneX(node.Lane), centerY, node);

        // 4. Refラベルの設定
        if (refLabels != null)
        {
            var labels = new List<FrameworkElement>();
            foreach (var refName in node.Commit.Refs)
            {
                labels.Add(CreateRefLabel(refName));
            }
            refLabels.ItemsSource = labels;
        }
    }

    private static double GetLaneX(int lane)
    {
        return lane * LaneWidth + LaneWidth / 2.0 + 4;
    }

    private static SolidColorBrush GetLaneBrush(int index)
    {
        return new SolidColorBrush(LaneColors[index % LaneColors.Length]);
    }

    private static Color GetLaneColor(int index)
    {
        return LaneColors[index % LaneColors.Length];
    }

    /// <summary>直線を描画します。</summary>
    private static void DrawLine(Canvas canvas, double x1, double y1, double x2, double y2,
        Color color, double thickness)
    {
        var line = new Line
        {
            X1 = x1, Y1 = y1,
            X2 = x2, Y2 = y2,
            Stroke = new SolidColorBrush(color),
            StrokeThickness = thickness,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
        };
        canvas.Children.Add(line);
    }

    /// <summary>Bézier曲線でレーン間の接続線を描画します。</summary>
    private static void DrawBezierEdge(Canvas canvas, double fromX, double fromY,
        double toX, double toY, Color color)
    {
        // コントロールポイントを計算して滑らかな曲線を作る
        var midY = (fromY + toY) / 2.0;

        var pathFigure = new Microsoft.UI.Xaml.Media.PathFigure
        {
            StartPoint = new Windows.Foundation.Point(fromX, fromY),
        };

        // S字カーブ: まずfromXで下がり、midYでtoXに遷移
        pathFigure.Segments.Add(new Microsoft.UI.Xaml.Media.BezierSegment
        {
            Point1 = new Windows.Foundation.Point(fromX, midY),
            Point2 = new Windows.Foundation.Point(toX, midY),
            Point3 = new Windows.Foundation.Point(toX, toY),
        });

        var pathGeometry = new Microsoft.UI.Xaml.Media.PathGeometry();
        pathGeometry.Figures.Add(pathFigure);

        var path = new Path
        {
            Data = pathGeometry,
            Stroke = new SolidColorBrush(color),
            StrokeThickness = LineThickness,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
        };

        canvas.Children.Add(path);
    }

    /// <summary>コミットドット（円）を描画します。</summary>
    private static void DrawCommitDot(Canvas canvas, double x, double y, GraphNode node)
    {
        var colorIndex = node.Lane;
        var color = GetLaneColor(colorIndex);

        // 外側の円（塗りつぶし）
        var dot = new Ellipse
        {
            Width = DotRadius * 2,
            Height = DotRadius * 2,
            Fill = new SolidColorBrush(color),
            Stroke = new SolidColorBrush(color),
            StrokeThickness = 1.5,
        };

        Canvas.SetLeft(dot, x - DotRadius);
        Canvas.SetTop(dot, y - DotRadius);
        canvas.Children.Add(dot);

        // マージコミットは二重円にする
        if (node.Commit.ParentShas.Count > 1)
        {
            var innerDot = new Ellipse
            {
                Width = DotRadius * 1.2,
                Height = DotRadius * 1.2,
                Fill = new SolidColorBrush(Color.FromArgb(255, 30, 30, 30)), // 暗い背景
                StrokeThickness = 0,
            };
            Canvas.SetLeft(innerDot, x - DotRadius * 0.6);
            Canvas.SetTop(innerDot, y - DotRadius * 0.6);
            canvas.Children.Add(innerDot);
        }
    }

    /// <summary>Refラベル（HEAD、ブランチ名、タグ名）のBorderを生成します。</summary>
    private static Border CreateRefLabel(string refName)
    {
        Color bgColor;
        if (refName == "HEAD")
            bgColor = HeadColor;
        else if (refName.StartsWith("tag:"))
            bgColor = TagColor;
        else
            bgColor = BranchColor;

        var border = new Border
        {
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(5, 1, 5, 1),
            Margin = new Thickness(0, 0, 2, 0),
            Background = new SolidColorBrush(bgColor),
        };

        var text = new TextBlock
        {
            Text = refName.StartsWith("tag: ") ? refName["tag: ".Length..] : refName,
            FontSize = 10,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Colors.White),
            VerticalAlignment = VerticalAlignment.Center,
        };

        border.Child = text;
        return border;
    }

    /// <summary>VisualTree から名前で子要素を検索します。</summary>
    private static T? FindChild<T>(DependencyObject parent, string name) where T : FrameworkElement
    {
        if (parent == null) return null;

        var childCount = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < childCount; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);

            if (child is T found && found.Name == name)
                return found;

            var result = FindChild<T>(child, name);
            if (result != null) return result;
        }
        return null;
    }
}
