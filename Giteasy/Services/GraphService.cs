using System.Collections.Generic;
using System.Linq;
using Giteasy.Models;

namespace Giteasy.Services;

/// <summary>
/// コミット履歴からグラフノード（レーン配置・エッジ情報）を構築するサービス。
/// VSCode Git Graph と同等以上のクオリティを目指すレーンアルゴリズム。
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

        // アクティブレーン: 各レーンが「次にどのSHAのコミットを待っているか」を保持
        // null = 空きレーン
        var lanes = new List<string?>();

        // カラーインデックス管理: レーンインデックス → カラーインデックス
        var laneColors = new Dictionary<int, int>();
        var nextColorIndex = 0;
        var maxLane = 0;

        // SHA → コミットインデックスのマッピング（高速検索用）
        var shaToIndex = new Dictionary<string, int>();
        for (int i = 0; i < commits.Count; i++)
            shaToIndex[commits[i].FullSha] = i;

        for (int idx = 0; idx < commits.Count; idx++)
        {
            var commit = commits[idx];
            var node = new GraphNode(commit) { Index = idx };

            // ── 1. このコミットのレーン位置を決定 ──
            int commitLane = -1;

            // 既存のレーンでこのSHAを待っているものを探す
            for (int l = 0; l < lanes.Count; l++)
            {
                if (lanes[l] == commit.FullSha)
                {
                    commitLane = l;
                    break;
                }
            }

            if (commitLane == -1)
            {
                // 新しいブランチの先頭 → 空きレーンを探すか新規追加
                commitLane = FindEmptyLane(lanes);
                if (commitLane == lanes.Count)
                    lanes.Add(null);
                if (!laneColors.ContainsKey(commitLane))
                    laneColors[commitLane] = nextColorIndex++;
            }

            node.Lane = commitLane;

            // このコミットが到着したので、レーンの予約を一旦解除
            lanes[commitLane] = null;

            // ── 2. 同じSHAを待っている他のレーン（マージの合流先）を処理 ──
            // 合流する追加レーンを見つけ、IncomingEdgesとして記録
            for (int l = 0; l < lanes.Count; l++)
            {
                if (l != commitLane && lanes[l] == commit.FullSha)
                {
                    // このレーンもこのコミットに合流する
                    var colorIdx = laneColors.GetValueOrDefault(l, 0);
                    node.IncomingEdges.Add(new GraphEdge(l, commitLane, colorIdx));
                    lanes[l] = null; // レーン解放
                }
            }

            // ── 3. 親コミットへのエッジを構築 ──
            for (int i = 0; i < commit.ParentShas.Count; i++)
            {
                var parentSha = commit.ParentShas[i];
                int parentLane;

                // 親SHAが既にどこかのレーンで待たれているか
                int existingParentLane = -1;
                for (int l = 0; l < lanes.Count; l++)
                {
                    if (lanes[l] == parentSha)
                    {
                        existingParentLane = l;
                        break;
                    }
                }

                if (existingParentLane >= 0)
                {
                    // 親は既に別コミットからレーン予約されている（マージの合流点）
                    parentLane = existingParentLane;
                }
                else if (i == 0)
                {
                    // 第一親 → 同じレーンを引き継ぐ
                    parentLane = commitLane;
                    lanes[parentLane] = parentSha;

                    // 色はコミットレーンの色を引き継ぐ
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
                    if (!laneColors.ContainsKey(parentLane))
                        laneColors[parentLane] = nextColorIndex++;
                }

                var colorIndex = (i == 0)
                    ? laneColors.GetValueOrDefault(commitLane, 0)
                    : laneColors.GetValueOrDefault(parentLane, 0);

                node.Edges.Add(new GraphEdge(commitLane, parentLane, colorIndex));
            }

            // ── 4. アクティブレーン一覧を記録（パススルー線の描画用） ──
            for (int l = 0; l < lanes.Count; l++)
            {
                if (lanes[l] != null && l != commitLane)
                    node.ActiveLanes.Add(l);
            }

            if (lanes.Count > maxLane) maxLane = lanes.Count;
            nodes.Add(node);

            // 末尾の空きレーンのみ除去（レーン安定性のため、途中の空きは除去しない）
            TrimTrailingEmptyLanes(lanes);
        }

        // ── 5. IncomingEdgesを対応ノードに設定 ──
        // 各ノードのEdgesを見て、次に描画されるノードのIncomingEdgesに追加する
        // GraphServiceで直接設定（描画側で参照する）
        for (int i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            if (i + 1 < nodes.Count)
            {
                var nextNode = nodes[i + 1];
                foreach (var edge in node.Edges)
                {
                    // このエッジは node の行の下半分から始まり、
                    // nextNode の行の上半分に到着する
                    // → nextNode の IncomingEdges として到着側の情報を追加
                    // ただし直線の場合（同レーン）で nextNode が実際にその親の場合のみ
                    // 一般化：全エッジを nextNode の incoming として追加
                    nextNode.IncomingEdges.Add(new GraphEdge(edge.ToLane, edge.ToLane, edge.ColorIndex));
                }
            }
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

    /// <summary>末尾の空きレーンだけを除去します（途中の空きは維持してレーン安定性を確保）。</summary>
    private static void TrimTrailingEmptyLanes(List<string?> lanes)
    {
        while (lanes.Count > 0 && lanes[^1] == null)
            lanes.RemoveAt(lanes.Count - 1);
    }
}
