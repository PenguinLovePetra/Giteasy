using System.Collections.Generic;
using System.Linq;
using Giteasy.Models;

namespace Giteasy.Services;

/// <summary>
/// コミット履歴からグラフノード（レーン配置・エッジ情報）を構築するサービス。
/// </summary>
public static class GraphService
{
    /// <summary>
    /// コミットリストからグラフノードを構築します。
    /// コミットは新しい順（日付降順）で渡されることを想定しています。
    /// </summary>
    public static List<GraphNode> BuildGraph(List<CommitInfo> commits)
    {
        if (commits == null || commits.Count == 0)
            return new List<GraphNode>();

        var nodes = new List<GraphNode>();

        // 現在アクティブなレーン: レーンインデックス → 期待するコミットSHA
        // null = 空きレーン
        var lanes = new List<string?>();

        // SHA → レーンインデックスのマッピング
        // (次にこのSHAのコミットが来たときにどのレーンに配置するか)
        var shaToLane = new Dictionary<string, int>();

        // カラーインデックス管理: レーンインデックス → カラーインデックス
        var laneColors = new Dictionary<int, int>();
        var nextColorIndex = 0;

        var maxLane = 0;

        foreach (var commit in commits)
        {
            var node = new GraphNode(commit);

            // 1. このコミットのレーン位置を決定
            int commitLane;
            if (shaToLane.TryGetValue(commit.FullSha, out var existingLane))
            {
                commitLane = existingLane;
                shaToLane.Remove(commit.FullSha);
            }
            else
            {
                // 新しいブランチの先頭 → 空きレーンを探すか新しいレーンを追加
                commitLane = FindEmptyLane(lanes);
                if (commitLane == lanes.Count)
                    lanes.Add(null);
                if (!laneColors.ContainsKey(commitLane))
                    laneColors[commitLane] = nextColorIndex++;
            }

            node.Lane = commitLane;
            lanes[commitLane] = null; // このコミットが配置されたのでレーンの予約を解除

            // 2. 親コミットへのエッジを構築
            for (int i = 0; i < commit.ParentShas.Count; i++)
            {
                var parentSha = commit.ParentShas[i];
                int parentLane;

                if (shaToLane.TryGetValue(parentSha, out var pLane))
                {
                    // 親は既に別コミットからレーン予約されている（マージの合流）
                    parentLane = pLane;
                }
                else if (i == 0)
                {
                    // 第一親 → 同じレーンを引き継ぐ
                    parentLane = commitLane;
                    lanes[parentLane] = parentSha;
                    shaToLane[parentSha] = parentLane;
                    if (!laneColors.ContainsKey(parentLane))
                        laneColors[parentLane] = laneColors.GetValueOrDefault(commitLane, nextColorIndex++);
                }
                else
                {
                    // マージの第二親以降 → 新しいレーンを割り当て
                    parentLane = FindEmptyLane(lanes);
                    if (parentLane == lanes.Count)
                        lanes.Add(null);
                    lanes[parentLane] = parentSha;
                    shaToLane[parentSha] = parentLane;
                    if (!laneColors.ContainsKey(parentLane))
                        laneColors[parentLane] = nextColorIndex++;
                }

                var colorIdx = laneColors.GetValueOrDefault(
                    i == 0 ? commitLane : parentLane, 0);
                node.Edges.Add(new GraphEdge(commitLane, parentLane, colorIdx));
            }

            // 3. アクティブレーン一覧を記録（パススルー線の描画用）
            for (int l = 0; l < lanes.Count; l++)
            {
                if (lanes[l] != null && l != commitLane)
                    node.ActiveLanes.Add(l);
            }

            if (lanes.Count > maxLane) maxLane = lanes.Count;
            nodes.Add(node);

            // 空きレーンを末尾から除去（描画幅の最適化）
            while (lanes.Count > 0 && lanes[^1] == null)
                lanes.RemoveAt(lanes.Count - 1);
        }

        // 最大レーン数を全ノードに設定
        foreach (var n in nodes)
            n.MaxLaneCount = maxLane;

        return nodes;
    }

    /// <summary>空きレーン（null）を探します。見つからなければリストのサイズを返します。</summary>
    private static int FindEmptyLane(List<string?> lanes)
    {
        for (int i = 0; i < lanes.Count; i++)
        {
            if (lanes[i] == null) return i;
        }
        return lanes.Count;
    }
}
