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
    private static readonly Color RemoteColor = Color.FromArgb(255, 198, 120, 221);

    private const double LaneWidth = 14.0;
    private const double DotRadius = 4.0;
    private const double LineThickness = 2.0;
    private const double RowHeight = 40.0;
    private const double GraphPaddingLeft = 8.0;

    public HistoryPage(HistoryViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        CommitListView.ItemsSource = _vm.GraphNodes;
    }

    private bool _eventsRegistered;

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        _vm.SetXamlRoot(XamlRoot);
        if (!_eventsRegistered)
        {
            _vm.PropertyChanged += (s, args) =>
            {
                if (args.PropertyName == nameof(HistoryViewModel.IsBusy))
                    LoadingRing.IsActive = _vm.IsBusy;
            };
            _eventsRegistered = true;
        }
        await _vm.RefreshAsync();
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
        => await _vm.RefreshAsync();

    private void CommitList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var selected = (CommitListView.SelectedItem as GraphNode)?.Commit;
        _vm.SelectedCommit = selected;
        RevertBtn.IsEnabled = selected != null;
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

            // Canvas をクリアして再描画
            canvas.Children.Clear();

        // グラフ列の幅を動的に調整
        var graphWidth = Math.Max(3, node.MaxLaneCount + 1) * LaneWidth + GraphPaddingLeft * 2;
        if (canvas.Parent is Grid parentGrid && parentGrid.ColumnDefinitions.Count > 0)
            parentGrid.ColumnDefinitions[0].Width = new GridLength(graphWidth);
        canvas.Width = graphWidth;
        canvas.Height = RowHeight;

        var centerY = RowHeight / 2.0;
        var commitX = GetLaneX(node.Lane);

        // ── 1. パススルーレーンの完全な縦線を描画（行全体を上から下まで） ──
        foreach (var activeLane in node.ActiveLanes)
        {
            var x = GetLaneX(activeLane);
            var color = GetLaneColor(activeLane);
            DrawLine(canvas, x, 0, x, RowHeight, color, LineThickness);
        }

        // ── 2. このコミットのレーンの上半分の線を描画（前のノードからの接続） ──
        // 最初のコミットでない限り、自分のレーンの上から中央までの線を引く
        bool hasUpwardConnection = false;

        // IncomingEdgesにこのレーンへの到着があるか確認
        foreach (var incoming in node.IncomingEdges)
        {
            if (incoming.FromLane == node.Lane && incoming.ToLane == node.Lane)
            {
                hasUpwardConnection = true;
                break;
            }
        }

        // ActiveLanesに自分自身は含まれないが、上から接続がある場合は縦線を引く
        if (hasUpwardConnection || node.Index > 0)
        {
            // 前のノードがこのレーンを使っていた場合は上半分の線を引く
            var prevNodeIdx = node.Index - 1;
            if (prevNodeIdx >= 0 && prevNodeIdx < _vm.GraphNodes.Count)
            {
                var prevNode = _vm.GraphNodes[prevNodeIdx];
                // 前のノードのEdgesの中に、このレーンに到着するエッジがあるか確認
                bool connectedFromAbove = prevNode.Edges.Any(e => e.ToLane == node.Lane) ||
                                           prevNode.ActiveLanes.Contains(node.Lane) ||
                                           prevNode.Lane == node.Lane;
                if (connectedFromAbove)
                {
                    var color = GetLaneColor(node.Lane);
                    DrawLine(canvas, commitX, 0, commitX, centerY, color, LineThickness);
                }
            }
        }

        // ── 3. 上からの合流線（異なるレーンから来るIncomingEdges） ──
        foreach (var incoming in node.IncomingEdges)
        {
            if (incoming.FromLane != node.Lane)
            {
                var fromX = GetLaneX(incoming.FromLane);
                var toX = GetLaneX(node.Lane);
                var color = GetLaneColor(incoming.ColorIndex);
                // 上半分にベジェ曲線で合流
                DrawBezierEdge(canvas, fromX, 0, toX, centerY, color);
            }
        }

        // ── 4. 下方向のエッジ（親コミットへの分岐/直進） ──
        foreach (var edge in node.Edges)
        {
            var fromX = commitX;
            var toX = GetLaneX(edge.ToLane);
            var color = GetLaneColor(edge.ColorIndex);

            if (edge.FromLane == edge.ToLane)
            {
                // 同じレーン → 下半分の直線
                DrawLine(canvas, fromX, centerY, toX, RowHeight, color, LineThickness);
            }
            else
            {
                // 異なるレーン → ベジェ曲線で分岐
                DrawBezierEdge(canvas, fromX, centerY, toX, RowHeight, color);
                // 分岐先のレーンの下半分直線も描画（次の行でパススルーになるため）
                // ただし次の行で描画されるアクティブレーンで処理される
            }
        }

        // ── 5. コミットドットを描画（最前面） ──
        DrawCommitDot(canvas, commitX, centerY, node);

        // ── 6. Refラベルの設定 ──
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
        catch (Exception ex)
        {
            // 描画エラーでアプリをクラッシュさせない
            Services.GitLogService.Log($"[グラフ描画エラー] {ex.Message}");
        }
    }

    private static double GetLaneX(int lane)
    {
        return lane * LaneWidth + LaneWidth / 2.0 + GraphPaddingLeft;
    }

    private static Color GetLaneColor(int index)
    {
        return LaneColors[Math.Abs(index) % LaneColors.Length];
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
        var pathFigure = new Microsoft.UI.Xaml.Media.PathFigure
        {
            StartPoint = new Windows.Foundation.Point(fromX, fromY),
        };

        // 滑らかなS字カーブ
        var controlOffset = Math.Abs(toY - fromY) * 0.6;
        pathFigure.Segments.Add(new Microsoft.UI.Xaml.Media.BezierSegment
        {
            Point1 = new Windows.Foundation.Point(fromX, fromY + controlOffset),
            Point2 = new Windows.Foundation.Point(toX, toY - controlOffset),
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
            StrokeLineJoin = PenLineJoin.Round,
        };

        canvas.Children.Add(path);
    }

    /// <summary>コミットドット（円）を描画します。</summary>
    private static void DrawCommitDot(Canvas canvas, double x, double y, GraphNode node)
    {
        var colorIndex = node.Lane;
        var color = GetLaneColor(colorIndex);
        var isMerge = node.Commit.ParentShas.Count > 1;

        var radius = isMerge ? DotRadius + 1.0 : DotRadius;

        // 外側の円
        var dot = new Ellipse
        {
            Width = radius * 2,
            Height = radius * 2,
            Fill = new SolidColorBrush(color),
            Stroke = new SolidColorBrush(color),
            StrokeThickness = 0,
        };

        Canvas.SetLeft(dot, x - radius);
        Canvas.SetTop(dot, y - radius);
        canvas.Children.Add(dot);

        // マージコミットは内側にドーナツ穴
        if (isMerge)
        {
            var innerRadius = radius * 0.45;
            // テーマに応じた背景色でドーナツ穴を描画
            var bgBrush = Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue(
                "LayerFillColorDefaultBrush", out var res) && res is SolidColorBrush brush
                ? brush
                : new SolidColorBrush(Color.FromArgb(255, 30, 30, 30));
            var innerDot = new Ellipse
            {
                Width = innerRadius * 2,
                Height = innerRadius * 2,
                Fill = bgBrush,
                StrokeThickness = 0,
            };
            Canvas.SetLeft(innerDot, x - innerRadius);
            Canvas.SetTop(innerDot, y - innerRadius);
            canvas.Children.Add(innerDot);
        }
    }

    /// <summary>Refラベル（HEAD、ブランチ名、タグ名）のBorderを生成します。</summary>
    private static Border CreateRefLabel(string refName)
    {
        Color bgColor;
        string displayName;
        string iconGlyph;

        if (refName == "HEAD")
        {
            bgColor = HeadColor;
            displayName = "HEAD";
            iconGlyph = "\uE72A"; // チェックマーク
        }
        else if (refName.StartsWith("tag:"))
        {
            bgColor = TagColor;
            displayName = refName.StartsWith("tag: ") ? refName["tag: ".Length..] : refName[4..];
            iconGlyph = "\uE8EC"; // タグ
        }
        else
        {
            bgColor = BranchColor;
            displayName = refName;
            iconGlyph = "\uE8AD"; // ブランチ
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

        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 3,
        };

        var icon = new FontIcon
        {
            Glyph = iconGlyph,
            FontSize = 9,
            Foreground = new SolidColorBrush(bgColor),
        };

        var text = new TextBlock
        {
            Text = displayName,
            FontSize = 10,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = new SolidColorBrush(bgColor),
            VerticalAlignment = VerticalAlignment.Center,
        };

        panel.Children.Add(icon);
        panel.Children.Add(text);
        border.Child = panel;
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
