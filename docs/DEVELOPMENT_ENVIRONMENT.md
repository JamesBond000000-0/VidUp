# Avalonia開発環境のセットアップ

## この作業環境での制約（2026-10-24）

Linux x64で `dotnet` は未導入。SDK配布元 `builds.dotnet.microsoft.com` と
`api.nuget.org` への接続はSSL接続エラーとなり、SDK・NuGet依存を取得できませんでした。
本書の環境定義・スクリプトは追加済みですが、コンテナーのビルドや.NETビルドの成功は未確認です。
ネットワーク制限を迂回する非公式SDKやパッケージ配布元は使用しません。

以下は必要な配布元にアクセスできるローカルPC/CI用の手順です。
Windows版WPFの実機検証はWindowsが必要で、Linuxコンテナーでは代替できません。

## 1. Windows（GUI検証の推奨環境）

1. Windows 11 x64へ.NET **10 SDK**を導入します。Runtimeだけではビルドできません。
   wingetがある場合はPowerShellで次を実行できます。

   ```powershell
   winget install --id Microsoft.DotNet.SDK.10 --exact
   ```

2. ターミナルを開き直し、リポジトリのルートで実行します。

   ```powershell
   ./scripts/check-desktop.ps1
   dotnet run --project VidUp.Desktop/VidUp.Desktop.csproj -c Release --no-build
   ```

スクリプト実行が組織のポリシーで制限されている場合は、ポリシーを変更せず、
スクリプト中の `dotnet restore` / `build` / `test` を個別に実行してください。
IDEは必須ではありません。VS Code等を使う場合も.NET 10に対応したものを使用してください。

## 2. Dev Container（Linux共通ビルド環境）

前提: Docker互換環境、VS CodeのDev Containers拡張、インターネット接続。
コンテナー用のDockerエンジン/権限はホスト側で用意します。

1. リポジトリをVS Codeで開き、「Dev Containers: Reopen in Container」を実行。
2. SDKイメージとAvaloniaのネイティブ依存を取得し、テストプロジェクトのrestoreを自動実行。
3. コンテナー内ターミナルで実行:

   ```sh
   bash scripts/check-desktop.sh
   ```

SDKは公式 `mcr.microsoft.com/dotnet/sdk:10.0-noble`、日本語フォントはNoto CJKを使用します。
`global.json` に合う.NET 10 SDKを利用します。イメージタグはセキュリティ更新を受けるため可変で、
完全固定が必要な段階では検証済みdigestへ固定してください。
NuGetキャッシュは専用Dockerボリュームに保持し、Gitには入れません。

### GUIの扱い

コンテナーはビルド/テストが主目的です。VNC/noVNCサーバーやホストX11への接続を
自動で公開しません。GUIを目で見るにはWindowsの上記手順を推奨します。

コンテナーに含めたXvfbで、ディスプレイのない環境でも起動を試せます:

```sh
xvfb-run -a dotnet run --project VidUp.Desktop/VidUp.Desktop.csproj -c Release --no-build
```

これは仮想画面でアプリを起動するだけで、画面はブラウザに表示されません。
終了はCtrl+C。起動しただけでは画面操作やIMEの検証を済ませたことにはなりません。

## 3. GitHub Actions

`.github/workflows/desktop-preview.yml` がWindows/Linuxでビルド・テストを実行し、
Windowsで自己完結型 `win-x64` プレビューを成果物にします。
PR・対象ブランチへのpush、または `workflow_dispatch` がトリガーです。

CI定義をGitHubへpushした後に実行できます。ローカルでファイルを追加しただけでは走りません。
成果物はActionsの `VidUp-Desktop-Preview-win-x64` から取得できます。
これは未署名の試作アプリであり、正式インストーラーでも認証済みリリースでもありません。

## 4. 必要な通信先

- Windows SDK導入: wingetが参照する公式Microsoft配布元。
- コンテナー: `mcr.microsoft.com` とそのイメージ配信先、Ubuntuパッケージミラー。
- SDK直接導入: Microsoftの.NET SDK配布元。
- パッケージ復元: `api.nuget.org` とNuGetが参照するパッケージ配信先。
- CI: GitHub Actions、SDK/パッケージの配布先。

制限付きネットワークでは管理者へ公式配布先の許可を依頼してください。
資格情報、トークン、署名鍵をスクリプトへ書き込まないでください。

## 5. 検証の区別

| 確認 | わかること | 別途必要なこと |
|---|---|---|
| `check-desktop` | 依存復元、C#/XAMLビルド、ViewModelテスト | 実画面の見栄え/操作 |
| Xvfbで起動 | 仮想X11上の起動可否 | 画面レビュー、IME、DPI、OS連携 |
| Windows実機 | 実GUI・IME・DPI・ファイル選択 | 実アップロード（現PoCは未実装） |

GUI確認は [検証・引き継ぎチェックリスト](AVALONIA_VERIFICATION_CHECKLIST.md) に沿って実施し、末尾のテンプレートへ結果を記録してください。
