@echo off
chcp 65001 > nul
echo =======================================================
echo GitEasyのMSIXインストーラーを作成します
echo =======================================================

echo 古いビルド成果物を削除しています...
if exist "Giteasy\bin" rmdir /s /q "Giteasy\bin"
if exist "Giteasy\obj" rmdir /s /q "Giteasy\obj"
if exist "Giteasy (Package)\bin" rmdir /s /q "Giteasy (Package)\bin"
if exist "Giteasy (Package)\obj" rmdir /s /q "Giteasy (Package)\obj"

echo.
echo =======================================================
echo Visual Studio 2022 での発行を推奨します。
echo コマンドラインでのビルドを試行しますが、環境によっては失敗する場合があります。
echo その場合は、Visual Studio で「Giteasy (Package)」プロジェクトを
echo 右クリック -^> 「発行」 -^> 「アプリ パッケージの作成」を実行してください。
echo =======================================================
pause

dotnet publish "Giteasy\Giteasy.csproj" -c Release -p:Configuration=Release -p:Platform=x64 -p:UapAppxPackageBuildMode=SideloadOnly -p:AppxBundle=Never -p:GenerateAppxPackageOnBuild=true -p:AppxPackageSigningEnabled=false

echo.
echo =======================================================
echo 完了しました。
echo =======================================================
pause
