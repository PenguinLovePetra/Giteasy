# Services

## 📁 役割と責務

Git 操作、データ永続化、ユーティリティなどのビジネスロジックを提供するサービス群です。
ViewModel から呼ばれ、View から直接参照されることはありません。

## ファイル構成

### Git 操作コア

| ファイル           | 説明                                                                                                             |
| ------------------ | ---------------------------------------------------------------------------------------------------------------- |
| `IGitBackend.cs`   | Git 操作の共通インターフェース。全バックエンドが実装する契約                                                     |
| `GitService.cs`    | メイン Git サービス。LibGit2Sharp をデフォルトバックエンドとして使用し、git.exe に切替可能。`IGitBackend` を実装 |
| `GitExeBackend.cs` | git.exe を `Process.Start` で呼び出すバックエンド。SSH 認証やクレデンシャルヘルパーが自動的に利用される          |
| `BackendModes.cs`  | バックエンドモード定数（`"builtin"` / `"system"`）                                                               |

### 補助サービス

| ファイル              | 説明                                                                                                   |
| --------------------- | ------------------------------------------------------------------------------------------------------ |
| `GitExeDetector.cs`   | PC 上の `git.exe` を検出する静的ユーティリティ。`where git` コマンドで検索し結果をキャッシュ           |
| `GitLogService.cs`    | Git コマンド実行ログの記録。`ObservableCollection` で UI バインド可能。スレッドセーフ                  |
| `GraphService.cs`     | コミット履歴からグラフノード（レーン配置・エッジ情報）を構築。Git Graph 可視化用                       |
| `DatabaseService.cs`  | SQLite によるデータ永続化。プロジェクト CRUD と設定 KVS を提供                                         |
| `AutoCloneService.cs` | `FileSystemWatcher` で監視ディレクトリ内の新規 bare リポジトリを検出し、自動クローン＆プロジェクト登録 |

## 設計方針

### デュアルバックエンド戦略

```
ViewModel → GitService (IGitBackend)
                 ├── LibGit2Sharp (builtin) — デフォルト、ライブラリ内蔵
                 └── GitExeBackend (system) — SSH認証自動利用、日本語パス対応
```

- `GitService` は各メソッドの先頭で `_externalBackend` の有無をチェックし、設定されていれば委譲
- `GitExeBackend` は `ProcessStartInfo` で git.exe を呼び出し、出力をパース
- 両バックエンドは `IGitBackend` インターフェースを満たす

### データベース設計

SQLite に2テーブル:

- **Projects** — 登録済みプロジェクト情報（Id, Name, LocalPath, RemoteUrl, CreatedAt, LastOpenedAt）
- **Settings** — Key-Value ストア（テーマ、バックエンド選択、AutoClone設定等）

## ⚠️ 技術的負債

- `GitService` が `IGitBackend` を実装しつつ内部でバックエンド委譲も行う二重構造
- `DatabaseService` は接続プーリングなし（毎回 `new SqliteConnection`）。現状の規模では問題なし
- `GitExeBackend` が `LibGit2Sharp.FileStatus` 列挙型を共有参照している（Models の `FileChange` 経由）

## 🔧 拡張ガイド

- **新しい Git 操作の追加**: `IGitBackend` にメソッド定義 → `GitService` と `GitExeBackend` の両方に実装
- **新しいサービスの追加**: このディレクトリに作成。名前空間は `Giteasy.Services`。`MainWindow.xaml.cs` でインスタンス化
- **設定の追加**: `DatabaseService.GetSetting` / `SetSetting` を使用（キー名の命名規則: `snake_case`）
