# ViewModels

## 📁 役割と責務

MVVM パターンの ViewModel 層。View（Page）と Service の間を仲介し、UI ロジック・コマンド・状態管理を担います。
`CommunityToolkit.Mvvm` の `ObservableObject` を基底クラスとし、`[ObservableProperty]` / `[RelayCommand]` 属性を活用しています。

## ファイル構成

| ファイル                | 対応ページ    | 説明                                                                            |
| ----------------------- | ------------- | ------------------------------------------------------------------------------- |
| `ProjectsViewModel.cs`  | ProjectsPage  | プロジェクト一覧の表示・切替・削除                                              |
| `RepoSetupViewModel.cs` | RepoSetupPage | リポジトリの新規作成（init）・クローン                                          |
| `AutoCloneViewModel.cs` | AutoClonePage | bare リポジトリ監視＆自動クローン。`AutoCloneLogEntry` モデルも同ファイルに定義 |
| `StatusViewModel.cs`    | StatusPage    | 変更ファイル一覧・ステージング・コミット・変更破棄                              |
| `BranchViewModel.cs`    | BranchPage    | ブランチ一覧・作成・チェックアウト・マージ・削除                                |
| `SyncViewModel.cs`      | SyncPage      | Fetch / Pull / Push                                                             |
| `HistoryViewModel.cs`   | HistoryPage   | コミット履歴・グラフ表示・Revert                                                |
| `SettingsViewModel.cs`  | SettingsPage  | リポジトリパス・ユーザー情報・テーマ・バックエンド設定                          |
| `LogViewModel.cs`       | LogPage       | Git 操作ログの表示・クリア                                                      |

## 設計方針

- **1 View : 1 ViewModel** の対応関係
- **コマンドパターン**: 非同期操作は `[RelayCommand]` + `async Task` メソッドで実装。`IsBusy` プロパティで多重実行を防止
- **イベント通知**: ViewModel 間の連携は `event Action` を使用（例: `ProjectOpened`, `SetupCompleted`, `ThemeChanged`）
- **XamlRoot 注入**: `SetXamlRoot(XamlRoot)` メソッドで ContentDialog 表示用の参照を取得
- **エラーハンドリング**: 全コマンドで try-catch → `DialogHelper` でユーザーフレンドリーなダイアログ表示

## ⚠️ 技術的負債

- `AutoCloneLogEntry` が `AutoCloneViewModel.cs` 内に定義されている（Models に移動すべき可能性あり）
- 一部の ViewModel が `Window` 参照を保持（`FolderPicker` の `InitializeWithWindow` 用）。テスタビリティの低下要因

## 🔧 拡張ガイド

- **新 ViewModel の追加**: `ObservableObject` を継承 → 名前空間 `Giteasy.ViewModels` → `MainWindow.xaml.cs` でインスタンス化
- **コマンドの追加**: `[RelayCommand]` 属性付き `private async Task XxxAsync()` メソッドを定義。XAML から `{x:Bind ViewModel.XxxCommand}` でバインド
