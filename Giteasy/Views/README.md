# Views

## 📁 役割と責務

MVVM パターンの View 層を管理するディレクトリです。
実際のページコンテンツは `Pages/` サブディレクトリに格納されています。

## ディレクトリ構成

```
Views/
└── Pages/          # 各画面の XAML + コードビハインド
    ├── ProjectsPage.xaml/.cs
    ├── RepoSetupPage.xaml/.cs
    ├── AutoClonePage.xaml/.cs
    ├── StatusPage.xaml/.cs
    ├── BranchPage.xaml/.cs
    ├── SyncPage.xaml/.cs
    ├── HistoryPage.xaml/.cs
    ├── SettingsPage.xaml/.cs
    └── LogPage.xaml/.cs
```

## 設計方針

- 各ページは `MainWindow` の `NavigationView` から遷移して表示される
- ページのコンストラクタで ViewModel を受け取り、UI 初期化時に `SetXamlRoot()` を呼び出す
- XAML では `{x:Bind ViewModel.Property}` によるコンパイル時バインディングを使用

## 🔧 拡張ガイド

- **新ページの追加**: `Pages/` に `XxxPage.xaml` + `XxxPage.xaml.cs` を作成 → 対応する ViewModel を `ViewModels/` に作成 → `MainWindow.xaml` の NavigationView に項目追加
