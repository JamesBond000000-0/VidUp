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
- [x] 既存テストの実行 → §7 の Wine 検証で解決（発見47件すべて合格）
- [ ] 起動時の .bak 回復 MessageBox の実表示と、実データでの保存・終了の通し確認

## 7. Wine による実行検証（2026-10-23 実施、Phase 2 完了後）

Linux VM 上に Wine 9.0（Ubuntu 24.04 パッケージ）+ Xvfb の環境を構築し、Windows 実機なしで実行検証を行った。環境の詳細と再現手順（プレフィックス、必須環境変数、VM 再起動時の復旧方法）は開発機の TOOLS.md に記録。

確認できたこと:
- Wine プレフィックス（win64）に Windows 版 .NET 10 SDK 10.0.100 を配置し、`dotnet --list-sdks` / ランタイム一覧（NETCore / AspNetCore / WindowsDesktop 10.0.0）を確認。
- 最小の WPF アプリ（net10.0-windows、Window と Application の生成）が Wine 上でビルド・実行できる。
- **VidUp.Test を vstest で実行し、発見された 47 件すべて合格（Failed: 0）**。プレフィックス再作成後も含め3回再現。ソース上の [Test] 属性は 48 件あり、1 件は NUnit に発見されない（本家時点からの特性で、本フォークの変更とは無関係。要調査事項として残す）。
- GitHub Actions 製インストーラ（v1.17.1-fork.1）の収録物を innoextract で展開し、その VidUp 本体を Wine で起動。初回起動の初期化が完了し、Documents\VidUp に settings.json（既定値）・accountlist.json・画像フォルダ群が正しく生成されることを確認。

環境上の制約・注意:
- vstest↔testhost の通信は `DOTNET_SYSTEM_NET_DISABLEIPV6=1` が必須（無いと Wine の IPv4 マップ IPv6 ソケットで通信が化け、ハンドシェイク直後に切断される。実測で切り分け済み）。接続タイムアウトは `VSTEST_CONNECTION_TIMEOUT=600000` を推奨。
- 32bit 実行はこの VM では使えない（wine32 導入で 64bit 側まで segfault するようになり、purge で復旧）。そのため 32bit の Inno インストーラ本体は実行できず、上記の展開による検証で代替した。インストーラ自体の実行確認は実機のみ。
- Wine 上のビルドは非常に遅い（小さなアプリでも初回約10分）。ビルドは Linux 側（EnableWindowsTargeting）で行い、実行だけ Wine に回すのが速い。

残る実機確認事項:
- [ ] 画面の見た目（MaterialDesignThemes 5.4.0 の表示）と対話操作
- [ ] インストーラの実行（.NET 10 未導入機での判定・導入）とアンインストール

## 8. Phase 3 実施結果（2026-10-23、アップロードまわり P0: BUG-03/05/06/15）

変更（BUG 単位で4コミット）:
- BUG-03: ThrottledBufferedStream の充填タスクを try/catch/finally 化。失敗時は例外を保持して必ず CompleteAdding し、消費側は保持例外を IOException として送出（ハングさせない）。Dispose で CompleteAdding + バッファ Dispose を行い、待ち手を解放する。テスト用に Stream を受け取るコンストラクタを追加。
- BUG-05: 再開照会の応答分類を ResumePositionEvaluator に切り出し。308 は Range から再開位置、200/201 は動画 ID を持つ完了として成功経路へ合流（データ再送なし）。全バイト到達時は不正な Content-Range を作らず、確定用の照会 PUT で ID を受け取る（計画の「PUT を送らず完了扱い」を ID 取得込みの形に調整）。照会用 StreamContent は using 化。
- BUG-06（最小修正）: UploadStats.Update の WaitOne〜Set を try/finally 化し、currentUpload の null ガードを追加。Uploader.timerElapsed はスナップショット＋null 許容にした。本格置換は Phase 6（BUG-09 と同時）。
- BUG-15: ストリームの差し替え・終了を setStream/clearStream に集約し、差し替え時は旧インスタンスを必ず Dispose、終了は finally で保証。フィールドは進捗表示の static アクセサのため static のまま、ライフサイクルの所有権のみ明示した。

検証:
- 新規テスト14件を追加（ストリーム5・応答分類8・統計イベント1）。Wine 上の全テスト実行で **61/61 合格**（従来47 + 新規14）。
- 全7プロジェクト 0 エラー。警告数はベースラインと完全一致（UI 171 / Test 176 / Youtube 7 ほか）。
- 実アカウントでのアップロード確認は未実施（分類ロジックはモック相当の単体テストで担保）。

## 9. Phase 4 実施結果（2026-10-23、スケジュール: BUG-07/11/12/22）

変更（BUG 単位で4コミット）:
- BUG-07: 週次・月次（日付ベース）で、頻度の端数により対象週/月へ進めた場合も曜日/日付の探索が問い合わせ位置から始まり、対象期間の前半が飛んで次回日時が1周期遅れていた。進めた場合は探索を期間先頭から再開するよう修正。あわせて月末補正の DaysInMonth を問い合わせ月でなく探索対象月で取るよう修正。
- BUG-12: 退化ケースと月次フォールバックを修正。有効な曜日/日付が皆無（JSON 由来で到達可能）の場合は過去日付を返さず InvalidScheduleException。月次相対で組み合わせが空の場合は無限再帰（スタックオーバーフローでプロセス終了）せず例外化し、月送りは上限つきループ化。月次日付ベースのフォールバックにも月末補正を適用（30日指定で 2026-03-02 → 2026-02-28）。
- BUG-11: コピーコンストラクタが UploadedUntil / IgnoreUploadsBefore をコピーしておらず、テンプレート複製でスケジュール進捗が失われていた。両フィールドを追加。
- BUG-22: MonthRelativeCombination に値等価（Equals/GetHashCode）を実装。辞書キーの参照同一性で、リストと辞書のインスタンスが食い違うと KeyNotFoundException になっていた。乖離は通常操作でも生じる（組み合わせ無効化後も dayTimes エントリが残る仕様のため）。セッター/コピー側はエントリを「取り込まず保持」に統一し、黙った再有効化を避けた。時間スロット配列の長さガード等の防御も追加。

検証:
- 特性テストを先行作成し、修正前に各バグの失敗を確認（空組み合わせのスタックオーバーフローでテストホストが死ぬことも実測。そのケースは修正後のみ実行）。
- 新規テスト22件（次回日時12・退化ケース5・コピー2・相対組み合わせ3）。Wine 上の全テスト実行で **83/83 合格**（従来61 + 新規22）。
- 全7プロジェクト 0 エラー。警告数はベースラインと完全一致（Business 5 含む）。
