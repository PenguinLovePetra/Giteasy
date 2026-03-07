# Models

## 📁 役割と責務

アプリケーション全体で使用されるデータモデル（POCO / 値オブジェクト）を定義するディレクトリです。
View、ViewModel、Service の各レイヤーから参照されます。

## ファイル構成

| ファイル         | 説明                                                                                    |
| ---------------- | --------------------------------------------------------------------------------------- |
| `ProjectInfo.cs` | DB 登録済みプロジェクトの情報（ID, 名前, パス, リモートURL, 日時）                      |
| `FileChange.cs`  | 変更ファイル情報。`ObservableObject` を継承し `IsSelected` の双方向バインディングを提供 |
| `BranchInfo.cs`  | ブランチ名、正規名、HEAD/リモートフラグ。UIアイコン表示ロジックも内包                   |
| `CommitInfo.cs`  | コミット情報（SHA, メッセージ, 作成者, 日時, 親SHA, Ref一覧）                           |
| `GraphNode.cs`   | Git グラフ描画用ノード。レーン配置・エッジ・パススルーレーン情報を保持                  |
| `GraphEdge.cs`   | グラフの接続線。始点レーン、終点レーン、カラーインデックスを保持                        |
| `AppThemes.cs`   | テーマ定数（Light/Dark/Glass）と `ThemeOption` モデル。設定画面のドロップダウン用       |

## 設計方針

- **イミュータブル指向**: `BranchInfo`, `CommitInfo` はコンストラクタで全プロパティを設定し、変更不可
- **UI バインディング対応**: `FileChange` は `CommunityToolkit.Mvvm` の `ObservableObject` を継承し、チェックボックスとの双方向バインディングを実現
- **LibGit2Sharp 依存**: `FileChange.cs` は `LibGit2Sharp.FileStatus` 列挙型を使用。これにより `GitExeBackend` でも同じ enum を共有できる

## ⚠️ 技術的負債

- `FileChange` が `LibGit2Sharp.FileStatus` に直接依存しているため、LibGit2Sharp を完全に除去する場合は独自の enum への置換が必要

## 🔧 拡張ガイド

- **新モデルの追加**: このディレクトリに新しい `.cs` ファイルを追加。名前空間は `Giteasy.Models`
- **UI バインディングが必要な場合**: `ObservableObject` を継承し `[ObservableProperty]` 属性を使用
