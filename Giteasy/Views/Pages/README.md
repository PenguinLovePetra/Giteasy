# Views/Pages

## 📁 役割と責務

アプリケーションの各画面（ページ）の XAML レイアウトとコードビハインドを格納するディレクトリです。
各ページは対応する ViewModel をコンストラクタで受け取り、`{x:Bind}` でデータバインディングを行います。

## ファイル構成

| ページ          | ViewModel            | 説明                                                                                       |
| --------------- | -------------------- | ------------------------------------------------------------------------------------------ |
| `ProjectsPage`  | `ProjectsViewModel`  | 登録済みプロジェクトの一覧表示。プロジェクトの切替・削除                                   |
| `RepoSetupPage` | `RepoSetupViewModel` | リポジトリの新規作成（init + README + 初回コミット）とクローン。タブ形式で2機能を提供      |
| `AutoClonePage` | `AutoCloneViewModel` | bare リポジトリの自動監視＆クローン設定。クローン履歴の表示                                |
| `StatusPage`    | `StatusViewModel`    | ファイル変更一覧、チェックボックスによるステージング選択、コミットメッセージ入力、変更破棄 |
| `BranchPage`    | `BranchViewModel`    | ブランチ一覧のリスト表示、新規作成、チェックアウト、マージ、削除                           |
| `SyncPage`      | `SyncViewModel`      | Fetch / Pull / Push ボタンとステータス表示                                                 |
| `HistoryPage`   | `HistoryViewModel`   | コミット履歴の Git グラフ可視化。Canvas + ベジェ曲線による描画。Revert 機能                |
| `SettingsPage`  | `SettingsViewModel`  | リポジトリパス、ユーザー名/メール、テーマ選択、バックエンド切替                            |
| `LogPage`       | `LogViewModel`       | Git 操作の実行ログ表示。ListView + ObservableCollection によるリアルタイム更新             |

## 設計方針

- **コードビハインド最小化**: ロジックは ViewModel に集中。コードビハインドは初期化と特殊な UI 操作（グラフ描画等）のみ
- **Loaded イベント**: 各ページは `Loaded` イベントで `ViewModel.SetXamlRoot(XamlRoot)` を呼び出し、ContentDialog 表示を可能にする
- **リフレッシュパターン**: ページ表示時に `Loaded` → `ViewModel.RefreshAsync()` で最新データを取得

### HistoryPage の特殊設計

`HistoryPage.xaml.cs` はグラフ描画のために大きなコードビハインドを持ちます:

- `Canvas` + `Path` + ベジェ曲線で Git グラフを描画
- `GraphNode` / `GraphEdge` データに基づくレーン配置
- カラーパレットによるブランチ色分け

## ⚠️ 技術的負債

- `HistoryPage.xaml.cs` が約400行のグラフ描画ロジックを含む（カスタムコントロール化を検討）

## 🔧 拡張ガイド

- **新ページの追加手順**:
  1. `XxxPage.xaml` — XAML レイアウト定義
  2. `XxxPage.xaml.cs` — コードビハインド（ViewModel 受取り、Loaded でリフレッシュ）
  3. `ViewModels/XxxViewModel.cs` — ビジネスロジック
  4. `MainWindow.xaml` — NavigationViewItem 追加（Tag="Xxx"）
  5. `MainWindow.xaml.cs` — `NavView_SelectionChanged` にケース追加
