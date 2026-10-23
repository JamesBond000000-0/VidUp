# Phase 0 ベースライン記録

記録日: 2026-10-23
対象コミット: `48bc132`（docs: バグ詳細と修正方針の計画書を追加。その親 `8d037ad` が本家と同一のコード）
環境: Linux (Ubuntu 24.04) 上の .NET SDK **10.0.100**（`~/dotnet10/dotnet`）で検証。
コードは net8.0 系ターゲットのまま（Phase 1 で移行予定）。

## 1. ビルド結果（全プロジェクト 0 エラー）

Release 構成、`--no-incremental` で実測。警告数は依存プロジェクト分を含む累計。

| プロジェクト | ターゲット | 結果 | 警告 |
|---|---|---|---|
| VidUp.Utils | net8.0 | 成功 | 0 |
| VidUp.Business | net8.0 | 成功 | 5（到達不能コード CS0162 ×4、未使用イベント CS0067 ×1） |
| VidUp.Json | net8.0 | 成功 | 7（依存分含む。自プロジェクト分は未使用変数 CS0168、未代入フィールド CS0649） |
| VidUp.Youtube | net8.0 | 成功 | 7（依存分含む） |
| VidUp.Setup | net8.0 | 成功 | 5（nullable 警告 CS8600/CS8602。Setup は唯一 Nullable 有効） |
| VidUp.UI | net8.0-windows7.0 | 成功（要 `-p:EnableWindowsTargeting=true`） | 171（大半は CA1416 プラットフォーム警告と依存分） |
| VidUp.Test | net8.0-windows | 成功（同上） | 176（依存分含む。自プロジェクト分は未使用変数 CS0219 ×5） |

所見:
- ベースライン時点で**ビルドエラーはゼロ**。Phase 1 以後の変更でエラーが出たら、それは変更由来と切り分けられる。
- 警告は既定で大量にあるため、「警告ゼロ」を合格条件にはしない。新規警告を増やさないことを目安にする。

## 2. 既存テスト（48件）の内訳と実行可否

| テストクラス | 件数 | 内容 |
|---|---|---|
| BasicTests | 12 | アップロード追加／削除の基本 |
| RemoveUploadsTests | 8 | 完了済みアップロードの削除 |
| TemplateImageFileHandlingTests | 8 | テンプレート画像のファイル処理（`[Order]` 付き） |
| ThumbnailFileHandlingTests | 20 | サムネイルのファイル処理（`[Order]` 付き） |
| 合計 | **48** | |

**実行はこの Linux 環境では不可**（実測で Test Run Aborted）:
- テストプロジェクトが `net8.0-windows` で、テスト対象が WPF（Microsoft.WindowsDesktop.App）に依存するため、実行には **Windows 実機＋.NET 8 Desktop Runtime** が必要。
- この環境には .NET 10 ランタイムしかなく、対象フレームワーク（.NET 8）自体も不在。

未決事項として記録:
- [ ] Windows 環境での48件実行結果（全件通過か）を別途確認し、ここへ追記する。
- Phase 2 / 4 で追加する読込・スケジュールのテストは、UI 非依存の形（Business / Json だけを参照するテスト）に寄せ、Windows 実機なしでも検証できる範囲を広げることを検討する（計画書 §7 の方針と整合）。

## 3. 壊れた JSON テスト資産

`VidUp.Test/TestConfigs/BrokenJson_*` の7ケースを用意した。内訳と期待動作は
`VidUp.Test/TestConfigs/BrokenJson_README.md` を参照。
現行コードでの各ケースの実際の振る舞い（落ちる／読める）は Phase 2 の着手時に実測して追記する。

## 4. Phase 1 への引き継ぎ

- 移行先: .NET 10（`net10.0` / `net10.0-windows7.0`）。SDK 10.0.100 で net8.0 のままビルドできることは本記録で確認済みのため、移行時の問題はターゲット変更とパッケージ更新由来に限定できる。
- 移行時に同時に直すもの（計画書 Phase 1）: インストーラ `setup.iss` の .NET 判定と案内文、`VidUp.Setup/Program.cs` の `net6.0-windows` 残骸パス。
- 移行後の合格条件: 本書 §1 と同じ手順で全プロジェクト 0 エラー、警告数が大きく増えないこと。
