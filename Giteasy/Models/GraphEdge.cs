namespace Giteasy.Models;

/// <summary>
/// グラフの接続線（エッジ）。親コミットへの線を表す。
/// </summary>
public class GraphEdge
{
    /// <summary>このコミットのレーン（始点）。</summary>
    public int FromLane { get; set; }

    /// <summary>親コミットのレーン（終点）。</summary>
    public int ToLane { get; set; }

    /// <summary>色インデックス（カラーパレットのインデックス）。</summary>
    public int ColorIndex { get; set; }

    public GraphEdge(int fromLane, int toLane, int colorIndex)
    {
        FromLane = fromLane;
        ToLane = toLane;
        ColorIndex = colorIndex;
    }
}
