@echo off
chcp 65001 > nul
echo =======================================================
echo GitEasyを持ち運び可能（ポータブル）形式で発行します
echo （開発環境や追加ランタイム不要で動く形式です）
echo =======================================================

dotnet publish "Giteasy\Giteasy.csproj" ^
  -c Release ^
  -r win-x64 ^
  --self-contained true ^
  -o "PublishOutput_win-x64"

echo.
echo =======================================================
echo ビルドが完了しました！
echo 「PublishOutput_win-x64」フォルダの中身をすべて、
echo 対象のPCにコピーして「Giteasy.exe」を起動してください。
echo =======================================================
pause
