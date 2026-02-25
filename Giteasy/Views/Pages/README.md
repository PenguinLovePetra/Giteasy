# Pages

## 📁 役割と機能 (Role & Functions)

`App.MainWindow` 内の NavigationView (メニュー) によって切り替え表示される、アプリケーションの各画面（Page）を配置するディレクトリです。

- **`BranchPage`** / **`HistoryPage`** / **`StatusPage`** / **`ProjectsPage`** / **`SettingsPage`** など:
  各機能ドメインごとのUIを定義しています。それぞれの Page は原則として 1 つの対応する ViewModel (`XxxViewModel`) を持ち、DataContext (または `x:Bind` のソース) として利用します。

## ⚠️ 技術的負債と既知の課題 (Technical Debt & Known Issues)

- **ViewModel のインスタンス化と依存注入 (DI)**:
  各 Page 自体は「自身がどの ViewModel インスタンスを使うべきか」を知らず、親である `MainWindow` からコンストラクタ経由で注入される密結合な設計になっています。
- **ナビゲーションのハードコード**:
  画面間遷移（例: ProjectsPage から SetupPage への遷移）において、NavigationView の構造（`MenuItems[1]` など）を知らないと遷移できない実装になっており、UIツリーの変更に対して非常に脆弱（脆い）です。
- **UI 操作（Canvas等）のロジック集中**:
  `HistoryPage` などのカスタム描画（Git Graphなど）を行う画面については、Canvas描画ロジックがコードビハインド（`HistoryPage.xaml.cs`）に大量に記述されており、肥大化しています。

## 📝 更新ルール (Update Rules)

**【重要】今後このディレクトリに新しい画面 (Page) を追加した際は、必ずこの README の「役割と機能」や、ナビゲーションに関わる「技術的負債」の情報を追記・修正を行ってください。**
