# Giteasy (プロジェクトルート)

## 📁 役割と機能 (Role & Functions)

GitEasy アプリケーション全体のエントリポイントおよびコアフロントエンド構成を管理するディレクトリです。

- **`App.xaml` / `App.xaml.cs`**:
  アプリケーションの起動ライフサイクル管理、DIコンテナ（現在は未使用、手動インスタンス化）、カスタムテーマの適用、およびグローバルな未処理例外（UnhandledException）のハンドリングを行います。
- **`MainWindow.xaml` / `MainWindow.xaml.cs`**:
  NavigationViewを使ったメインインターフェースのガワ（シェル）を提供します。ここで `GitService` 等のコアサービスを初期化し、各画面（Page）へのViewModelの受け渡しによる擬似DIを実現しています。

## ⚠️ 技術的負債と既知の課題 (Technical Debt & Known Issues)

- **手動の依存性注入 (Poor Man's DI)**:
  `MainWindow.xaml.cs` で全ViewModelとServiceを `new` して子Viewに渡す密結合な設計になっています。プロジェクト規模が拡大した場合、Microsoft.Extensions.DependencyInjection などの正式なDIコンテナの導入が必要です。
- **状態の肥大化**:
  `MainWindow` がほぼ全ての上位状態を抱え込んでおり、責務がやや過多（God Object化の兆候）です。

## 📝 更新ルール (Update Rules)

**【重要】今後このディレクトリ（プロジェクト直下）に新しいコア構成ファイルやアプリケーション設定を追加した際は、必ずこの README の「役割と機能」や「技術的負債」のリバースエンジニアリング情報に追記・修正を行ってください。**
