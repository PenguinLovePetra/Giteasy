# Giteasy — プロジェクトルート

## 📁 役割と責務

GitEasy アプリケーションのエントリポイントおよびメインシェル（ウィンドウ）を管理するディレクトリです。
WinUI 3 (Windows App SDK) + MVVM アーキテクチャで構築されています。

## ファイル構成

| ファイル                                 | 説明                                                                                                    |
| ---------------------------------------- | ------------------------------------------------------------------------------------------------------- |
| `App.xaml` / `App.xaml.cs`               | アプリケーションのライフサイクル管理、テーマ適用、グローバル例外ハンドリング                            |
| `MainWindow.xaml` / `MainWindow.xaml.cs` | NavigationView によるメインシェル。全 Service/ViewModel の初期化とページ遷移を管理                      |
| `Giteasy.csproj`                         | プロジェクト定義。NuGet パッケージ依存（LibGit2Sharp, CommunityToolkit.Mvvm, Microsoft.Data.Sqlite 等） |
| `app.manifest`                           | Windows アプリケーションマニフェスト                                                                    |

## 設計方針

### アーキテクチャ

```
App.xaml.cs → MainWindow.xaml.cs → Pages (View)
                   ↓
              ViewModels ← Services ← Models
```

- **MVVM パターン**: CommunityToolkit.Mvvm を使用。View は ViewModel をコンストラクタ経由で受け取る
- **手動 DI**: `MainWindow.xaml.cs` で全 Service/ViewModel を `new` して管理（Poor Man's DI）
- **デュアルバックエンド**: LibGit2Sharp（組込み）と git.exe（システム）を切替可能

### テーマシステム

3種類のテーマをランタイムで切替可能:

- **Light** — Mica バックドロップ + Light テーマ
- **Dark** — Mica バックドロップ + Dark テーマ
- **Glass** — DesktopAcrylic バックドロップ + Dark テーマ

## ⚠️ 技術的負債

- **手動 DI**: 全 ViewModel/Service を `MainWindow` で `new` している。規模拡大時は `Microsoft.Extensions.DependencyInjection` 等の導入を検討
- **状態の集中**: `MainWindow` が全上位状態を抱え込んでおり、責務過多の兆候がある

## 🔧 拡張ガイド

- **新しいページの追加**: `Views/Pages/` に Page を作成 → `ViewModels/` に ViewModel を作成 → `MainWindow.xaml` の NavigationView に項目追加 → `NavView_SelectionChanged` にケース追加
- **新しい Service の追加**: `Services/` に作成 → `MainWindow.xaml.cs` のコンストラクタでインスタンス化し、必要な ViewModel に渡す
