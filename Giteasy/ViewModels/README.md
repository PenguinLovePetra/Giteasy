# ViewModels

## 📁 役割と機能 (Role & Functions)

MVVM (Model-View-ViewModel) アーキテクチャにおける ViewModel 層を担当します。
`CommunityToolkit.Mvvm` パッケージを活用し、`ObservableObject` の継承によるプロパティ変更通知（`INotifyPropertyChanged`）と、`[RelayCommand]` によるコマンドバインディングを提供します。

- **状態管理 (State Management)**:
  `Services` 層から取得したデータ（BranchList, CommitHistory 等）を保持し、View（XAML）がバインディング可能な形に変換・提供します。
- **画面単位の分割**:
  各ナビゲーションページ（BranchPage, StatusPage, HistoryPage 等）に対して、対応する `XxxViewModel` が存在します。

## ⚠️ 技術的負債と既知の課題 (Technical Debt & Known Issues)

- **ビューへの強結合 (View Dependency)**:
  `RepoSetupViewModel` など一部の ViewModel が `XamlRoot` や `Window` オブジェクトへの直接的な参照を持っており（`SetXamlRoot` 等）、MVVMの原則である「Viewを知らないこと」から逸脱しています。ダイアログ表示において `IDialogService` への分離が必要です。
- **肥大化 (Fat ViewModel)**:
  `SettingsViewModel` や `MainWindow` に紐づく状態管理が、UIのロジックだけでなく一部のビジネスロジックまで抱え込む傾向にあります。
- **同期と非同期の混在**:
  `RelayCommand` で呼び出される非同期バックグラウンド処理（Git連携など）のタスク管理やキャンセル機構が不十分であり、連打対応（`IsBusy`フラグの一貫性）などが各VMごとに手動実装されています。

## 📝 更新ルール (Update Rules)

**【重要】今後このディレクトリに新しい ViewModel を追加した際は、必ずこの README の「役割と機能」や「技術的負債」に追記・修正を行ってください。View依存のコードを入れた場合はその理由も明記してください。**
