# Converters

## 📁 役割と機能 (Role & Functions)

XAMLでのデータバインディング時に、ViewModelのプロパティ値（bool, string, enum等）を表示用の型（Visibility, Brush, string等）に変換するための `IValueConverter` 実装クラスを配置するディレクトリです。

- **`BooleanToVisibilityConverter`** 等（現在実装されているものに合わせて追加）: データ駆動UIを支えるための基本的なパッシブ変換ロジックです。

## ⚠️ 技術的負債と既知の課題 (Technical Debt & Known Issues)

- **再利用性と乱立**:
  類似のコンバーター（例: `InverseBooleanConverter` と `BooleanToVisibilityConverter` の組み合わせ等）が散在する可能性があります。パラメータ化によって統一できるものは統合の余地があります。
- **型安全性の欠如**:
  `Convert` メソッドの引数は `object` 型であるため、キャストエラーが実行時まで判明しません。より厳密なViewModelバインディングによるConverter回避も検討すべきアーキテクチャ上の課題です。

## 📝 更新ルール (Update Rules)

**【重要】今後このディレクトリに新しいコンバーターを追加した際は、必ずこの README の「役割と機能」に用途を追記・修正を行ってください。人間が一目で変換ルールを把握できる状態を維持してください。**
