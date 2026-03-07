# Converters

## 📁 役割と責務

XAML データバインディングで使用される値コンバーター（`IValueConverter`）を定義するディレクトリです。
View 層でのみ参照され、Model/ViewModel の値を UI 表示用に変換します。

## ファイル構成

| クラス                        | 説明                                                                                               |
| ----------------------------- | -------------------------------------------------------------------------------------------------- |
| `HexColorToBrushConverter`    | Hex カラー文字列（例: `#4CAF50`）を `SolidColorBrush` に変換。ファイル変更ステータスの色表示に使用 |
| `InverseBoolConverter`        | `bool` 値を反転。`IsBusy` の逆で UI の有効/無効を切替える際に使用                                  |
| `StringToVisibilityConverter` | 空でない文字列なら `Visible`、空なら `Collapsed`。ステータスメッセージの表示制御に使用             |

※ 全コンバーターは `Converters.cs` に定義されています（1ファイル複数クラス）。

## 設計方針

- XAML リソースとして `App.xaml` に登録し、全ページから利用可能
- 再利用性の高い汎用コンバーターのみを定義

## 🔧 拡張ガイド

- **新コンバーターの追加**: `Converters.cs` に `IValueConverter` を実装するクラスを追加 → `App.xaml` にリソース登録
- **ファイル分割**: コンバーターが増えた場合は、クラスごとにファイルを分割することを検討
