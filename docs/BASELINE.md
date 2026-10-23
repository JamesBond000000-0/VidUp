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

## 5. Phase 1 結果（.NET 10 移行、2026-10-23）

変更内容:
- TFM: 5プロジェクト `net8.0` → `net10.0`、UI `net8.0-windows7.0` → `net10.0-windows7.0`、Test `net8.0-windows` → `net10.0-windows`
- パッケージ: MaterialDesignThemes 5.0.0 → **5.4.0**（net10.0-windows7.0 用 lib を含むことを実物で確認）、Newtonsoft.Json 13.0.3 → 13.0.4、MimeTypesMap 1.0.8 → 1.0.9、Microsoft.NET.Test.Sdk 17.9.0 → 18.11.0、NUnit 4.1.0 → **4.6.1**、NUnit3TestAdapter 4.5.0 → 5.2.0
  - NUnit は最新の 5.0.0 ではなく 4.x 最新の 4.6.1 を採用した。メジャーバージョン更新によるテストコード側の破壊的変更を Phase 1 に混ぜないため（アダプタ 5.2.0 は NUnit 4.x 対応の最終系統）。
- `global.json` を追加（SDK 10.0.100、rollForward: latestFeature）。
- インストーラ: `setup.iss` の判定を `IsNet10Installed` に変更（バージョンフォルダ先頭2文字が `10` かで判定。案内文とダウンロード URL も 10.0 に）。`VidUp.Setup/Program.cs` の残骸パス `net6.0-windows` → `net10.0-windows7.0`（2箇所）。

検証結果（§1 と同じ手順、SDK 10.0.100）:
- **全7プロジェクト 0 エラー**。`--no-incremental` の警告数は UI **171** / Test **176** で、移行前と**完全一致**（新規警告なし）。
- 生成された `VidUp.runtimeconfig.json` が `Microsoft.WindowsDesktop.App 10.0.0` を要求すること、出力フォルダが `net10.0-windows7.0` になり Program.cs の修正パスと一致することを確認。

未確認（Windows 実機が必要）:
- [ ] 既存テスト48件の実行（移行前後とも未実施。§2 の未決事項を引き継ぐ）
- [ ] MaterialDesignThemes 5.4.0 での画面表示確認（テーマ側の見た目の差分チェック）
- [ ] Inno Setup でのインストーラ生成と、.NET 10 未導入環境での判定メッセージ確認

## 6. Phase 2 結果（保存まわりの P0、2026-10-23）

対応 BUG: 01 / 13 / 04 / 21 / 23。コミットは BUG 単位で分けた（4d163c5、c909828、256a63a、61441ca）。

主な変更:
- 保存: 全メソッド try/finally 化、AtomicFileWriter による原子的書き込み（*.tmp → File.Replace、直前世代を *.bak に1世代保持）、スナップショットのロック内作成、全ファイルへの専用ロック、StopSerialization の冪等化、終了待ちへの10秒上限。
- 読込: 本文が解析不能なら .bak へフォールバックし、回復時は MainWindowViewModel が MessageBox で通知（アプリに全体向けステータス表示が無いため、既存コードでも使われている MessageBox を採用）。BUG-04 の補正は collect-then-apply 化し、補正先も「保持テンプレートのリストからの除去のみ」に是正（旧方式は補正が収束しなかった）。コンバータ類は未知ID・不正GUIDで例外を投げず、既定値／代替アカウント＋再シリアライズフラグに統一。明示 null は Upload/Template の [OnDeserialized] で正規化し、templatelist の決め打ち読み飛ばしは通常のデシリアライズに置換。

検証（検証ハーネスで VidUp.Json を直接実行。VidUp.Test は WPF 依存のため従来どおり実機待ち）:
- BrokenJson 資産6件すべてが例外なく読込成功（修正前は6件すべて例外で失敗することを実測済み）。正常な設定の読込結果は修正前と同一。
- 書込失敗の注入（*.tmp パスをディレクトリで占有）で、カウンタが戻る・元ファイル無傷・一時ファイル残留なしを確認。
- 並行保存ストレス（4種の保存を混ぜて300回並行実行）: エラー0、成果ファイルはすべて JSON として解析可能、カウンタも正常にドレイン。
- 切断した uploads.json は .bak から回復（件数復元）を確認。.bak 無しでは部分救済せず空として起動継続。
- 全7プロジェクト 0 エラー、警告数は移行後ベースラインと完全一致（UI 171 / Test 176 ほか）。

未確認（Windows 実機が必要）:
- [ ] 既存テスト48件の実行（Phase 0 からの継続事項）
- [ ] 起動時の .bak 回復 MessageBox の実表示と、実データでの保存・終了の通し確認
