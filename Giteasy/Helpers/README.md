# Helpers

## 📁 役割と機能 (Role & Functions)

アプリケーション全体で汎用的に利用される、特定の機能層（Model/View/ViewModel等）に完全に属さないユーティリティクラス群を配置するディレクトリです。

- **`DialogHelper`**:
  UIスレッド上でのダイアログ表示（エラー、情報、確認など）をラップして提供します。ViewModelなどが `XamlRoot` を前提にしてUI依存のポップアップを投げるために使用されます。

## ⚠️ 技術的負債と既知の課題 (Technical Debt & Known Issues)

- **Static メソッドの多用**:
  ヘルパーメソッドが多くの場合 `static` で実装されているため、単体テスト（モック化）が非常に困難です。
- **ViewModel からの UI 依存**:
  `DialogHelper` が `XamlRoot` を要求するため、ViewModel層がMicrosoft.UI.Xamlの存在を意識せざるを得ないアーキテクチャ違反（レイヤー汚染）が発生しています。本来的には `IDialogService` などのインターフェースを介して抽象化すべき部分です。
- **Helper の Fat 化（God Utility）**:
  分類に困ったコードがすべて "Helper" としてここに投げ込まれる「黒魔術化」のリスクが常にあります。

## 📝 更新ルール (Update Rules)

**【重要】今後このディレクトリに新しいユーティリティを追加した際は、その責務が本当に Helper であるべきかを再考し、実装した場合は必ずこの README の「役割と機能」や「技術的負債」に追記・修正を行ってください。**
