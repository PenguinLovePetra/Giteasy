using System.Collections.Generic;

namespace Giteasy.Models;

/// <summary>
/// グラフ描画用のノード。各コミットに対して1つ作成される。
/// </summary>
public class GraphNode
{
    /// <summary>このノードに対応するコミット情報。</summary>
    public CommitInfo Commit { get; }

    /// <summary>このコミットが配置されるレーン番号（0-based）。</summary>
    public int Lane { get; set; }

    /// <summary>このノードから下方向へ出るエッジ（親コミットへの接続）。</summary>
    public List<GraphEdge> Edges { get; } = new();

    /// <summary>上方向から来るエッジ（子コミットからの接続）。前の行で定義されたエッジの到着側。</summary>
    public List<GraphEdge> IncomingEdges { get; } = new();

    /// <summary>この行で描画すべきパススルーレーンの番号一覧。</summary>
    public List<int> ActiveLanes { get; } = new();

    /// <summary>全体で使用するレーン数の最大値（描画幅の計算用）。</summary>
    public int MaxLaneCount { get; set; }

    /// <summary>このノードのインデックス（コミットリスト内の位置）。</summary>
    public int Index { get; set; }

    /// <summary>上方向から親接続がある（既存レーンに待たれていた）かどうか。</summary>
    public bool HasParentAbove { get; set; }

    public GraphNode(CommitInfo commit)
    {
        Commit = commit;
    }
}
