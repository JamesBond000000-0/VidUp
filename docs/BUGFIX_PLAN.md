# VidUp バグ詳細と修正方針

対象: JamesBond000000-0/VidUp（fork 元 Drexel2k/VidUp）
基準コミット: master @ `8d037ad`（本家と同一、2026-10-23 確認）
調査範囲: 全プロジェクト約26,000行（Business / Json / Youtube / UI / Utils / Test）
状態: 静的解析で特定した潜在バグ。実機での再現確認は各フェーズの着手時に行う。
行番号は基準コミット時点のもの。

## 0. 進め方の原則

1. **修正はテーマ単位で小さく分ける**。1つの変更で「ハング修正」「スケジュール修正」「.NET 10 移行」を混ぜない。コミットはフェーズ内でも対象 BUG ごとに分け、コミットメッセージに「対象 BUG 番号と理由」を必ず残す（2026-10-23 決定）。作業は fork（JamesBond000000-0/VidUp）内のみで行い、本家へは送らない。
2. **バグ修正の前に特性テスト（characterization test）を書く**。特に Schedule は現行テストが皆無で、修正が正しいかを判定する物差しが先に必要。
3. **共通パターンで直す**。個別の場当たり対応ではなく、以下の3つの型を全体に適用する。
   - 型A「後始末は finally で」: カウンタ増減、イベント Set、ガード解除、省電力復帰、Timer 破棄。
   - 型B「ファイルは原子的（atomic）に書く」: 一時ファイルに書いてから差し替える。書き込み途中のファイルは正規パスに置かない。
   - 型C「待つ処理には必ずタイムアウト／キャンセルを付ける」: 無期限 WaitOne / Take / Wait は禁止。
4. **データ互換を壊さない**。既存ユーザーの JSON（uploads/templates/settings 等）が読み続けられることを各フェーズの合格条件にする。壊れた JSON のテスト資産を用意して読込テストに使う。
5. .NET 10 移行は別トラック（後述 §8）。バグ修正と相互にブロックしない。

## 1. 重要度の定義

| 区分 | 意味 |
|---|---|
| P0 Critical | アプリのハング、プロセス終了、データ破損、二重投稿など、通常使用で実害が出る |
| P1 High | 特定条件で上記に準ずる実害、または機能が恒常的に止まる |
| P2 Medium | 誤表示、競合、リーク、例外で処理全体が止まる等 |
| P3 Low | 計算誤差、検証の抜け、失敗が無言になる等 |

---

## 2. P0: ハング・データ破損・二重投稿

### BUG-01 JSON 保存の例外で「終了時ハング」＋「JSON 破損」が同時に起きる
- **場所**: `VidUp.Json/Content/JsonSerializationContent.cs`（6メソッド全部）、`VidUp.Json/Settings/JsonSerializationSettings.cs:31-78`、`VidUp.UI/MainWindow.xaml.cs:42-49`（呼出側）
- **詳細**: 保存開始時に `openWritesCount++` し、終了時に `--` と `CountdownEvent.Signal()` を行うが、`try/finally` が両ファイルとも **0件**。シリアライズ中に例外（ディスク満杯、ファイルロック、同時変更されたリストの列挙例外など）が出ると減算が飛び、カウンタが永遠に戻らない。終了処理はそのカウンタで作った `CountdownEvent` をタイムアウトなしで `Wait()` するため、ウィンドウが消えたのにプロセスが残り続ける。さらに書込先が最終パスへの直接上書き（`new StreamWriter(path)` で即座に切り捨て）のため、同じ例外でJSONが途中まで書かれた状態で残り、次回起動の読込に影響する。`StopSerialization()` を2回呼ぶと `openWritesCountDown` が差し替わり、進行中の書込が古いイベントに届かなくなる問題も同居。
- **再現条件**: 保存中に I/O 例外か列挙例外を1回起こすだけ。その後のアプリ終了でハング。
- **修正方針**:
  1. 全保存メソッドで増減と Signal を `try/finally` 化（型A）。
  2. 書込を型B化: 同一フォルダに一時ファイル（`*.tmp`）へ書き、`File.Replace` / `File.Move(overwrite)` で原子的に差し替える。差し替え前に直前世代を `*.bak` に残し、読込側は正規ファイルが壊れていたら `.bak` にフォールバックできるようにする。`.bak` は **1世代のみ**とし、フォールバックが発生したら既存のステータス表示でもユーザーに知らせる（データが過去状態に戻ったことに気づけるようにするため。2026-10-23 決定）。
  3. `StopSerialization` はイベントを1回だけ生成して保持し、二重呼出に耐える形に変更。終了側の `Wait()` には上限（例 10秒）を付け、超過したら Tracer に記録して終了を続行する（型C）。
- **検証**: 保存中に例外を注入するテスト（書込先をロック、列挙中に変更）で「カウンタが戻る」「元ファイルが無傷」「終了待ちが上限で抜ける」を確認。既存48テストの回帰。
- **規模感**: 中。保存層のみ、呼出側の変更は小。

### BUG-02 省電力無効化が別スレッドで解除される＋例外で復帰しない
- **場所**: `VidUp.UI/DllImport/PowerSavingHelper.cs:15-22`、`VidUp.UI/ViewModels/UploadRibbonViewModel.cs:682-735`（特に 695 / 716 / 733）
- **詳細**: `SetThreadExecutionState` はスレッド単位のAPI。無効化はUIスレッドで実行されるが、有効化（復帰）は `await ... ConfigureAwait(false)` の後なのでスレッドプールの別スレッドで実行され、UIスレッド側の設定が残り続ける。結果、アップロードが終わってもPCがスリープしなくなる（プロセス終了まで）。戻り値も未確認で失敗は無言。加えて `startUploadingAsync` 全体に try/finally がなく、`UploadAsync` 等が例外を投げると復帰処理に到達しないだけでなく `uploadStatus` が `Uploading` のまま固定され、そのセッションでは二度とアップロードを開始できなくなる。メソッドが `async void` のため例外は大域ハンドラ行き＝クラッシュダイアログになる。
- **修正方針**:
  1. 無効化／復帰を同一スレッドで行う。アップロード完了後の復帰呼び出しを UI スレッドへ戻してから実行するか、ス레ッドに紐付かない仕組みに置換する（実行状態を保持する専用スレッドを用意する方式でも可）。
  2. `startUploadingAsync` を `try/finally` で包み、finally で「省電力復帰」「uploadStatus の復旧」「uploader の破棄」を必ず行う（型A）。
  3. `SetThreadExecutionState` の戻り値を Tracer に記録する。
- **検証**: 正常終了・例外・ユーザー停止の3経路で、終了後にスリープ抑止が解除されることを確認（手動確認＋コードレビュー）。例外経路の単体テスト（Uploader をモック化できる形に薄く分離する）。

### BUG-03 ThrottledBufferedStream の充填タスク例外でアップロードが無言ハング
- **場所**: `VidUp.Youtube/VideoUploadService/ThrottledBufferedStream.cs:180, 255-287, 292-303, 344-353`、`VidUp.Youtube/Http/HttpHelper.cs:33`（アップロード用 HttpClient は Timeout 無限大）
- **詳細**: バッファ充填は `Task.Run(this.fillMemoryBuffer)` の投げっぱなし。`memoryBuffer.CompleteAdding()` は「読み切った（bytesRead==0）」ときだけ呼ばれる。途中でファイル削除・ディスクエラー・破棄による Dispose が起きると例外でタスクが死に、消費側の `memoryBuffer.Take()` が永久に待つ。HttpClient 側もタイムアウト無限大なので、ユーザーが手動で止めるまで固まる。逆向きもあり、消費側が離脱すると充填側が容量1のバッファへの `Add` で永久ブロックし、最大100MBのバッファとスレッドを占有し続ける。`Dispose` でも `CompleteAdding` もバッファの Dispose も行っていない。
- **修正方針**:
  1. 充填タスクに `try/catch/finally` を付け、例外時は例外情報を保持した上で必ず `CompleteAdding()` する。消費側は Take 後／完了時に保持された例外があればそれを送出して失敗として終わらせる（ハングさせない）。
  2. `Dispose(bool)` で `CompleteAdding()` とバッファ Dispose を行い、充填タスクが `Add` で待っている場合に抜けられるようにする。
  3. アップロード全体のキャンセル（BUG-09）と連動させ、停止時にこの待ちも解けるようにする。
- **検証**: 途中で例外を出す元ストリームを使った単体テストで「Read が例外で返る（ハングしない）」ことをタイムアウト付きで確認。

### BUG-04 自動補正が列挙中のコレクションを変更して起動時に落ちる
- **場所**: `VidUp.Json/Content/JsonDeserializationContent.cs:443-465`（本体 448-456）、関連 `VidUp.Business/Template.cs:243,475`、`Upload.cs:91-121`
- **詳細**: 読込後の整合性チェック `checkUploadTemplateEqualsContainingTemplate` が `foreach (Upload upload in template.Uploads)` の最中に `upload.Template = null`（setter 自身が元のテンプレートから RemoveUpload する）と `template.RemoveUpload(upload)` を呼ぶため、列挙子が無効化され次の `MoveNext()` で `InvalidOperationException`。つまり「不整合データを直すための処理」が不整合データで必ず落ち、起動時の読込全体が失敗する。二重削除（setter 側と明示側）も起きている。
- **修正方針**:
  1. まず不整合な組をリストに収集し、列挙が終わってからまとめて付け替える（collect-then-apply）。
  2. 削除は片側だけに寄せる（`upload.Template = null` の setter に任せ、明示の RemoveUpload は消す）。
  3. 補正した場合は ReSerialize フラグを立て、直した状態で保存し直す（現行の意図を維持）。
- **検証**: わざと不整合にした uploads/templates の JSON 資産で読込が成功し、付け替え結果が期待通りになるテストを追加。

### BUG-05 アップロード再開照会が 200/201 をエラー扱いし、二重投稿を招く
- **場所**: `VidUp.Youtube/VideoUploadService/YoutubeVideoUploadService.cs:364, 369-375, 386`
- **詳細**: resumable upload の再開位置照会で「308 Permanent Redirect 以外は全て HttpStatusException」にしている。しかしサーバ側で受信が完了している場合、Google は 200/201 を返す。その場合も例外化されるため3回リトライ後に Failed 確定となり、実際には動画が存在するのに失敗表示→ユーザーの再試行で重複投稿になり得る。さらに照会結果から `BytesSent == ファイル長` になった場合、次の PUT で開始＞終了の不正な Content-Range（ContentLength=0）を作る分岐がある。照会用の StreamContent が `using` なしで破棄漏れしている点も同居。
- **修正方針**:
  1. 200/201 は「アップロード完了済み」として扱い、返された動画IDで成功経路に合流させる。
  2. `BytesSent >= ファイル長` の場合は PUT を送らず完了扱いにするガードを追加。
  3. 照会用 StreamContent を `using` 化。
- **検証**: 照会応答の分岐（308 / 200 / 201 / エラー）をモック応答で単体テスト化。実アカウントでの確認は任意（モックで十分）。

### BUG-06 Uploader の Timer 競合と resetEvent の Set 漏れ
- **場所**: `VidUp.Youtube/Uploader.cs:171-175, 243, 262-288`、`VidUp.Youtube/UploadStats.cs:136-155`、`YoutubeVideoUploadService.cs` 内の `resetEvent.WaitOne()` 計10箇所
- **詳細**: (a) Timer は `dueTime=0` で作られ、`this.currentUpload = upload` の代入より前にコールバックが発火し得る。コールバックは null チェックなしで `currentUpload` を参照するため NullReferenceException。Timer コールバック（プールスレッド）の未処理例外はプロセス終了につながる。終了処理で `uploadStats = null` にした後も、実行中のコールバックが到達して NRE になる競合もある（Timer.Dispose は実行中コールバックを待たない）。(b) `UploadStats.Update` は `resetEvent.WaitOne()` と `Set()` の間に try/finally がなく、途中で例外が出ると二度と Set されず、サービス側の全 WaitOne が永久ブロックする。そもそも async 経路で同期ブロックする設計自体が脆い。
- **修正方針**:
  1. Timer 生成を `currentUpload` 代入の後に移し、コールバック冒頭で対象のスナップショットを取り null を許容する。停止時はコールバック完了を待つ／世代カウンタで古いコールバックを無効化する。
  2. `Update` の WaitOne〜Set を try/finally 化（型A）。
  3. resetEvent 方式の本格置換は Phase 6 で BUG-09 と合わせて行う（2026-10-23 決定）。このフェーズでは最小限の finally 化までに留める。置換の内訳: 「止める」は CancellationToken の全フェーズ伝播、「統計更新の排他」は `SemaphoreSlim.WaitAsync(token)` への移行（キャンセル可能な非同期ロックになり、停止時にロック待ちも解ける）。
- **検証**: 開始直後・終了直後にタイマーが発火するケースを再現するテスト（間隔を注入可能にする等の小さな変更を含む）。

---

## 3. P1: 機能が止まる・スケジュールが狂う

### BUG-07 週次／月次スケジュールが1周期分余計に先送りされる
- **場所**: `VidUp.Business/Schedule.cs` `weeklyGetNextDate` 1452-1475、`monthlyGetNextDateMonthDateBased` 1480-1514
- **詳細**: 頻度（例: 隔週）で「対象でない週」を次の対象週へ繰り上げた後も、探索開始位置が「問い合わせ日の曜日／日」のままで、その対象週の中の早い曜日を取りこぼす。フォールバックで更にもう1周期進む。
  - 例（週次）: 隔週・月曜のみ有効、水曜に問い合わせ → 本来は次の対象週の月曜が返るべきところ、更に2週間後の月曜が返る。
  - 例（月次）: 隔月・5日有効、2月20日に問い合わせ → 本来3月5日が、5月5日になる。
  影響先は `Upload.autoSetPublishAtDateTimeInternal`（Upload.cs:775,782）経由の自動 PublishAt で、公開日時が無言で数週間〜数か月遅れる。
- **修正方針**: 周期を繰り上げた場合は、その周期の先頭（週初／月初）から探索し直すよう開始位置をリセットする。月次相対方式（L1522以降）は同型の欠陥がないことを確認済みなので、実装を揃える形にする。
- **検証**: **修正前に特性テストを書く**。頻度1〜3 × 有効曜日／日の組合せ × 問い合わせ日 の表形式テストで、現行の誤りケースと正しいケースの両方を固定してから直す。

### BUG-08 自動取込（FileSystemWatcher）の消費ループが詰まり、止まらない
- **場所**: `VidUp.UI/ViewModels/MainWindowViewModel.cs:59-60, 504-583, 1014-1017`
- **詳細**: フォルダ監視で検出したファイルを `BlockingCollection` 経由で処理する消費者が `async void` の無限ループで、キャンセルも破棄もできない。先頭ファイルを開けるまで30秒間隔で無限リトライ（存在確認も上限もなし）するため、削除された／書き込みが終わらないファイル1つで後続の全ファイルが止まる。`Close()` は Tracer を閉じるだけで、Watcher 群・Collection・ループ・後処理用 CancellationTokenSource を何も止めない。また `AddFiles` をプールスレッドから直接呼んでおり、画面側コレクションへの通知が UI スレッド外から届く（WPF の CollectionChanged 例外の温床）。
- **修正方針**:
  1. 消費者を `Task`＋`CancellationToken` で回し、`Close()` で「Watcher 停止 → CompleteAdding → トークン取消 → ループ終了待ち」の順に片付ける。
  2. リトライに上限（回数と総時間）とファイル存在確認を入れ、上限超過時は Tracer に記録してそのファイルをスキップし次へ進む（head-of-line blocking の解消）。
  3. `AddFiles` 呼び出しを Dispatcher 経由にする。
- **検証**: ロックされ続けるファイル＋正常ファイルの順で流し、正常側が処理されること、Close 後にスレッドと Watcher が残らないことを確認。

### BUG-09 キャンセルが本送信にしか効かず、トークンも使い回せない
- **場所**: `VidUp.Youtube/VideoUploadService/YoutubeVideoUploadService.cs:83, 174, 188, 301-303, 369, 531` ほか各サービス、`VidUp.Youtube/Http/HttpHelper.cs:31-33`
- **詳細**: CancellationToken を渡しているのは本送信 PUT のみで、初期化・再開照会・サムネイル・プレイリスト追加は素の `SendAsync`（既定100秒タイムアウト頼み）。「停止」が効かないフェーズがある。また `Uploader` の tokenSource は生成が1回だけで、Cancel 後の再利用では以後のアップロードが即「停止扱い」になる（現行 UI は毎回 `new Uploader` するため未顕在化の潜在バグ）。
- **修正方針**: 全 SendAsync にトークンを伝播。Uploader のセッション開始時に tokenSource を作り直す（旧ものは Dispose）。あわせて各クライアントに明示的なタイムアウト方針を持たせる（アップロード本体は無限のままでよいが、照会系は有限に）。**本項目と同時に、BUG-06 の resetEvent（AutoResetEvent）の本格置換も行う（2026-10-23 決定）**: 統計更新の排他を `SemaphoreSlim.WaitAsync(token)` に移し、アップロード側の WaitOne 10箇所を置き換える。これにより「停止」と「排他待ち」が分離され、停止時に排他待ちもキャンセルで解けるようになる。
- **検証**: 各フェーズで停止操作を行い、速やかに止まることを手動確認。Uploader 再利用の単体テスト追加。

### BUG-10 認証：再サインインで refresh_token が返らず詰む／リスナーのリークと無期限待ち
- **場所**: `VidUp.Youtube/AuthenticationService/YoutubeAuthentication.cs:69-88, 122-164, 219-228, 250-265`
- **詳細**: 認可URLに `access_type=offline`（と必要に応じ `prompt=consent`）がなく、Google が初回以外 refresh_token を返さないケースで `tokenEndpointDecoded["refresh_token"]` が KeyNotFoundException になり、「API応答エラーではない」汎用認証エラーに化けて再ログイン不能に見える。待ち受けの `HttpListener` は using なしで例外経路ではポートを掴んだまま残り、`GetContextAsync` はタイムアウト／キャンセルなし（ブラウザを閉じると無期限待ち）。トークン要求ボディは値が URL エンコードされていない。サインアウト処理もアカウント側のトークンを消すだけで HttpHelper のアクセストークンキャッシュを消さないため、有効期限内（最大約1時間）は API を呼べてしまう。
- **修正方針**:
  1. 認可URLに `access_type=offline` を追加。応答に refresh_token が無い場合は既存値を保持する（上書きで消さない）。
  2. HttpListener を using 化し、待ち受けにタイムアウト（または CancellationToken）を付ける。
  3. 要求ボディは `FormUrlEncodedContent` で構築する。
  4. サインアウト時に `HttpHelper.ClearAccessTokens()` 相当をそのアカウント分だけ呼ぶ。
- **検証**: トークン応答のパースを単体テスト化（refresh_token 欠落ケース）。実ログインでの確認は手動で1回。

### BUG-11 Schedule のコピーで進捗2項目が落ち、複製テンプレートが公開済み枠を再利用する
- **場所**: `VidUp.Business/Schedule.cs` コピーコンストラクタ 294-332（欠落項目は宣言 L17 相当の `UploadedUntil` と `IgnoreUploadsBefore`）、利用側 `VidUp.Business/Template.cs` のテンプレート複製
- **詳細**: コピーが `UploadedUntil` と `IgnoreUploadsBefore` をコピーしていないため、複製したテンプレートのスケジュール進捗が初期化され、既に公開済みの日時枠を再提案したり、無視すべき過去アップロードを再び対象にしたりする（PublishAt の重複・衝突）。
- **修正方針**: コピーコンストラクタに2項目を追加し、「全 [JsonProperty] 項目がコピーされる」ことを検証するテスト（元とコピーのシリアライズ結果比較）を追加して再発を防ぐ。
- **検証**: 上記テスト＋複製直後の GetNextDateTime が元と同じになること。

### BUG-12 月次の月末補正漏れと、空スケジュールでの無限再帰
- **場所**: `VidUp.Business/Schedule.cs:1508-1514`（月末補正なし）、`1522-1552`（相対方式の再帰）、`1472 / 1510`（有効日ゼロ時の -1）
- **詳細**: (a) 月次・日付指定のフォールバック経路には `DaysInMonth` によるクランプがなく、29/30日指定だと2月を飛ばして3月1日等に化ける（対象外の月への着地）。(b) 相対方式は組み合わせリストが空だと空の月を再帰で進み続け `StackOverflowException`（捕捉不能、プロセス終了）。空リストは UI ガードでは防げるが、JSON から直接読み込まれたデータでは到達可能で、読込時の `OnDeserializing` がリストを Clear するため「項目欠落した templatelist.json」がそのまま発火条件になる。(c) 有効な曜日／日が1つもない場合、`Array.IndexOf(..., true)` が -1 を返し、週初／月初の前日＝過去日時が「次の公開日時」として返る。
- **修正方針**: フォールバックにも月末クランプを適用。空リスト・有効日ゼロは「次の日時なし」として明示的に扱う（呼出側の既存の例外型 `InvalidScheduleException` を使う）。再帰は上限付きループに置換するか、空チェックを入口に置く。
- **検証**: BUG-07 と同じ表形式テストに月末（1/2月、うるう年）・空ケースを追加。

---

## 4. P2: 競合・リーク・例外伝播

### BUG-13 保存のロックが実質効いていない箇所
- **場所**: `VidUp.Json/Content/JsonSerializationContent.cs:70-88, 143-237, 270, 291-314`、`VidUp.Json/Automation/JsonSerializationUploadResultAutomationInfo.cs:23-33`
- **詳細**: (a) `SerializeUploadProgress` には専用ロック自体がなく、同時書込で共有違反 IOException や破損が起きる（そして BUG-01 を誘発）。(b) `SerializeAllUploads` はロック取得「前」にライブなリストを列挙してスナップショットを作るため、同時変更で列挙例外＋古いスナップショットが新しい内容を上書きし得る。(c) 他のメソッドも「ファイル単位のロック」だけで、オブジェクト自体の同時変更からは守られていない。(d) アカウント保存は公開オブジェクト自体をロック対象にしており他経路と連動しない。自動化用 JSON 書込も無ロック。
- **修正方針**: BUG-01 の atomic 化と同時に、スナップショット作成をロック内に寄せ、全ファイルに専用ロック（private object）を割り当てる。自動化用書込にも同じ型を適用。
- **検証**: 並行保存ストレステスト（複数スレッドで保存を回し、成果 JSON が常にパース可能であること）。

### BUG-14 EventAggregator の購読解除が無ロック＋強い参照のまま放置
- **場所**: `VidUp.UI/EventAggregation/EventAggregator.cs:11, 37-70`、`Subscription.cs:13-19`、購読側多数
- **詳細**: 購読解除クロージャだけロックを取らず、Publish のスナップショット作成と競合するとリスト破損／例外の恐れ。シングルトンがデリゲートを強参照し、返された `Subscription` を破棄していない購読が26箇所あり、再生成される ViewModel ではハンドラが重複登録されていく（重複削除・重複保存などの形で現れる）。Publish は1つのハンドラの例外で後続全員への配信が止まり、例外が発行側に伝播する。
- **修正方針**: 解除処理を同じロック内に。Publish はハンドラ単位で try/catch し、失敗は Tracer に記録して配信を続ける。主要な購読は Subscription を保持して Dispose する形に順次移行（まず生成・破棄が繰り返されるものから）。
- **検証**: 購読／解除／発行を並行に回す単体テスト（現在テストゼロの領域なので新規）。

### BUG-15 アップロード用ストリームの static 共有とリトライ時リーク
- **場所**: `VidUp.Youtube/VideoUploadService/YoutubeVideoUploadService.cs:27, 109, 148-149, 165`
- **詳細**: 送信用 `ThrottledBufferedStream` が static フィールドで無同期に共有され、進捗表示（別スレッド）からも読まれる。リトライ時に新インスタンスへ差し替えると、`using` の対象は最初のインスタンスだけなので、差し替え後は誰も Dispose せず、ファイルロック＋最大100MB＋50MBのバッファがリトライごとに残る。
- **修正方針**: static をやめてインスタンス保持にし、差し替え時は旧インスタンスを Dispose してから差し替える。進捗参照は保持中インスタンスへの参照をその都度読む形にする。
- **検証**: リトライを強制するモックテストで、終了後にファイルが開き直せる（ロックが残っていない）ことを確認。

### BUG-16 アクセストークンの static Dictionary が無同期
- **場所**: `VidUp.Youtube/Http/HttpHelper.cs:20, 46-79`
- **詳細**: チェックと取得が非原子的で、並行呼出（アップロードとプレイリスト取得など）や Clear との競合で KeyNotFoundException、Dictionary 内部破損、トークン取得の重複実行が起き得る。
- **修正方針**: `ConcurrentDictionary` 化、または取得経路を単一ロックで直列化。ついでに有効期限判定も同じ臨界区間に。
- **検証**: 並行取得の単体テスト。

### BUG-17 API 応答の Items / Id が null のまま参照される
- **場所**: `YoutubePlaylistService.cs:79`、`YoutubePlaylistItemService.cs:170`、`YoutubeVideoService.cs:51`、`YoutubeVideoUploadService.cs:187-188`
- **詳細**: YouTube API は結果0件のとき `items` 自体を返さないため DTO の配列が null になり、`.Length` / `foreach` で NullReferenceException。空プレイリスト、プレイリスト0件のアカウント、削除済み動画の照会で踏む。アップロード応答側もデシリアライズ結果が null だと NRE になり、「リトライ可能な送信失敗」として扱われて完了済みセッションへ再 PUT する（BUG-05 と複合して重複化）。
- **修正方針**: DTO 側で配列を空配列で初期化する＋サービス側の参照を null 条件付きにする、の二重防御。アップロード応答の null は「状態不明」として再開照会に回す。
- **検証**: items 欠落の応答 JSON 資産での単体テスト。

### BUG-18 プレイリスト自動設定 Timer が破棄されず、ガードも finally なし
- **場所**: `VidUp.UI/ViewModels/PlaylistRibbonViewModel.cs:37, 143, 280-537, 787, 808`
- **詳細**: `System.Threading.Timer` を誰も Dispose しないため、終了後もコールバックが API 呼出・JSON 保存・イベント発行をし得る。`Change()` の呼出元が3系統あり無同期。実行中ガード `autoSettingPlaylists` の解除が finally ではなく、末尾の保存／タイマ再設定／イベント発行で例外が出るとガードが立ったままになり、以後自動設定が二度と動かない（例外自体は async void 経由でクラッシュ方向）。
- **修正方針**: ViewModel の Close/Dispose で Timer を破棄し、Change と破棄の競合をロックで防ぐ。ガード解除を finally 化。コールバックからの処理は UI スレッドへの戻し方針を BUG-20 と揃える。
- **検証**: 末尾処理で例外を注入しても次回タイマーが動くこと、終了後にコールバックが来ないことの確認。

### BUG-19 自動化実行の AutoResetEvent 漏れで以降の自動化が全て止まる
- **場所**: `VidUp.UI/ViewModels/UploadRibbonViewModel.cs:64-67, 571-625, 739-787`
- **詳細**: `serializeandExecute` が `WaitOne()` と `Set()` の間に try/finally を持たず、対象ファイルの不在・実行不可・親フォルダなし（ファイル名だけの指定で `DirectoryInfo.Parent` が null）・ミリ秒単位のファイル名衝突などで例外が出ると Set が飛び、以降の自動化実行が全部永久待ちになる。あわせて `finishedUploads`（素の List）が複数スレッドから無ロックで変更・列挙・クリアされている。さらに中止経路の1箇所では有効チェックなしに実行パスが渡され、null/空でこの経路に入る。
- **修正方針**: WaitOne〜Set を try/finally 化（型A）。`finishedUploads` はロック付きアクセスに統一。ファイル名に衝突しない接尾辞（連番/Guid）を付ける。実行前チェックを全経路で共通化。
- **検証**: 存在しない実行ファイルを指定したケースで「エラー記録＋次回も動く」ことを確認。

### BUG-20 async void（19箇所）と ConfigureAwait(false) 後の UI 操作
- **場所**: `VidUp.UI` 全域（`MainWindowViewModel`、`PlaylistRibbonViewModel`、`UploadRibbonViewModel`、`TemplateRibbonViewModel`、`SettingsViewModel` ほか）
- **詳細**: イベント／コマンド入口が軒並み `async void` で、例外はすべて大域ハンドラ＝クラッシュになる。中には `ConfigureAwait(false)` の後にダイアログ結果のキャスト（`(bool)await DialogHost.Show(...)`：結果なしで閉じると InvalidCast/NullReference）や、コレクション変更を伴う処理・イベント発行をプールスレッドで実行する箇所があり、「コレクションは Dispatcher スレッド以外から変更できない」例外が断続的に出る構造。同じコードベース内に「GUIスレッドに戻らなければならない」というコメント付きの正しい書き方も混在している。
- **修正方針**: 一括置換はせず、触る機能のついでに個別に直す。(1) 例外を握って Tracer＋状態復旧する入口ラッパを用意し、async void の入口をそこへ寄せる。(2) `ConfigureAwait(false)` の後に UI へ触る箇所は ConfigureAwait を外すか Dispatcher へ戻す。(3) ダイアログ結果は `is bool` パターンで受ける。
- **検証**: 対象機能ごとの手動確認＋例外注入。段階実施のためフェーズは最後尾に置き、既存修正と混ぜない。

---

## 5. P2（続き）: 読込の堅牢性

### BUG-21 コンバータの例外で全データが読めなくなる
- **場所**: `VidUp.Json/Content/CategoryIdConverter.cs:17`、`CultureInfoCultureStringConverter.cs:20`、`YoutubeAccountGuidStringConverter.cs:12-22`、`UploadGuidStringConverter.cs:23`、`JsonDeserializationContent.cs:305-348, 371`
- **詳細**: 未知のカテゴリID（YouTube がカテゴリを増やすと起きる）や、現行OSのカルチャ一覧にない文化名で `.First()` が例外を投げ、1項目の不整合で全データ読込が失敗する。空／null のアカウント GUID は「代替アカウントに寄せる」既存の仕組みがあるのに、その手前で例外を投げている。`setTemplateOnUploads` も GUID の欠落・重複・形式不正で無防備に例外化する。なお同関数が失敗したときに立てる ReSerialize フラグが `TemplateList` 側になっており、本来書き直すべき `uploads.json` 側ではないため、壊れた参照が毎回起動時に再検出され続ける。
- **修正方針**: 各コンバータを「見つからなければ既定値／null＋ReSerialize フラグ」の方針に統一（プレイリストIDコンバータの既存の振る舞いが手本）。`setTemplateOnUploads` はレコード単位で try/catch し、直せないレコードはスキップして記録。フラグは AllUploads 側に正す。
- **検証**: 未知カテゴリ・壊れたGUID・項目欠落を含む JSON 資産で「読めるものは読める」テスト。

### BUG-22 MonthRelativeCombination の参照同一性による辞書キー不一致
- **場所**: `VidUp.Business/MonthRelativeCombination.cs`（Equals/GetHashCode なし）、`VidUp.Business/Schedule.cs:83-105, 406-419, 1292-1541`
- **詳細**: 値の等価性がないクラスを Dictionary キーに使っているため、リスト側のインスタンスと辞書側のキーが別物になると `KeyNotFoundException`。読込時の setter が「見つからなければ新規作成してキーにするが、リストには追加しない」経路を持ち、コピーの補助関数にも同じ作りの箇所（長さ0のケースが素通りする条件式ミスを含む）がある。高度な月次相対スケジュールで PublishAt 自動設定時に落ちる。通常経路は辞書に触らないため表面化しにくい。
- **修正方針**: `Equals`/`GetHashCode`（必要なら record 化）を実装して値同一にする。新規作成した組は必ずリストにも追加する。時間帯配列は長さ3前提をやめ、不足は null で埋める。
- **検証**: 読込→コピー→次回日時計算の一連を回す単体テスト。

### BUG-23 JSON の明示 null でコンストラクタ初期化が潰れる
- **場所**: `VidUp.Business/Upload.cs:151-213, 454`、`JsonDeserializationContent.cs:274-291`
- **詳細**: `"statusInformation": null` や `"tags": null` が JSON にあると、コンストラクタで用意した初期値が null で上書きされ、後段の `.Clear()` / `AsReadOnly()` が NullReferenceException。読込側も先頭3トークンを決め打ちで読み飛ばす実装で、プロパティ追加や `"templates": null` で位置がずれ、null のまま走査して落ちる。テンプレート側の JsonConstructor が空で `uploads` 未初期化のままになるケースもある。
- **修正方針**: 対象プロパティに null を弾く setter／読込後の正規化（null なら初期値に戻す）を入れ、チェック側（BUG-04 の修正後処理）に null 項目の除去を寄せる。読込側の決め打ち読み飛ばしは通常の Deserialize に置換する。
- **検証**: 各項目を null にした JSON 資産での読込テスト。

---

## 6. P3: 軽微だが確実な誤り

| ID | 場所 | 内容 | 修正方針 |
|---|---|---|---|
| BUG-24 | `VidUp.Utils/Constants.cs:5` | `ByteMegaByteFactor = 1048567`（正: 1048576）のタイポ。全 MB 表示が微妙にずれる | 定数を直し、使用箇所の表示テストを1件追加 |
| BUG-25 | `VidUp.Utils/QuarterHourCalculator.cs:7-18`、`Upload.cs:187-193, 784-786, 903` | 分が15の倍数だと秒が残ったまま返り、それ以外は秒が落ちる不一貫。`DateTime.Kind` も落ちる。スケジュール枠（秒=0）との完全一致比較で不一致→重複判定が外れることがある | 常に秒以下を切り捨てて15分単位に丸め、Kind を保持。利用箇所の比較は「分単位への正規化後」で行う |
| BUG-26 | `VidUp.Business/Upload.cs:918-924, 335-349` | サイズ上限チェックが遅延初期化前は常に0のフィールドを見ており実質デッドコード。上限超過でも自動 Paused にならない | チェック側でプロパティ経由に統一 |
| BUG-27 | `VidUp.Business/Upload.cs:802-804` | `SetPublishAtTime` が `PublishAt.Value` を無条件参照し、未設定だと NRE（隣の SetPublishAtDate は null 対応済み） | 同様の null ガードを追加 |
| BUG-28 | `VidUp.Youtube/UploadStats.cs:45, 56-74, 145-175` | 失敗したアップロードも総進捗に満額加算される（進捗の過大表示）。0バイトファイルや未開始状態でゼロ除算／NaN が int 化されて不定値 | 加算は実送信分だけに。分母0はガード。リスト変化時の累積リセット仕様を見直す |
| BUG-29 | `VidUp.Youtube/VideoUploadService/ThrottledBufferedStream.cs:33-66, 222, 245-250, 356-390` | スロットル履歴の列挙が一部ロック外で、古いエントリだけ残る状況で `Min()` 例外の可能性。速度合計の int Sum は約143MB/s 超の持続でオーバーフロー。スロットル判定が「読む前・過去分のみ」でバースト時に上限超過が持続する | 列挙をロック内に寄せ、合計は long 化。精度問題は仕様として記録し、必要なら判定位置を見直す |
| BUG-30 | `VidUp.Utils/Tracer.cs:9-23, 33` | 静的コンストラクタが Settings 未設定等で例外になると、その後すべての Tracer.Write が TypeInitializationException になる。古いトレース削除も取得順の Reverse 頼みで日付順になっていない | 初期化を遅延＋失敗しても書込側が落ちない作りに。削除はファイル名／更新日時でソートしてから |
| BUG-31 | `VidUp.UI/DllImport/ShutDownHelper.cs:38-50`、`SuspendStateHelper.cs` | P/Invoke の戻り値を全て無視し、トークンハンドルも閉じない。権限が取れないと「終了後シャットダウン」が無言で何も起きない | 戻り値を確認して失敗を Tracer に記録。ハンドルは CloseHandle する |
| BUG-32 | `VidUp.Youtube/StatusInformationCreatorYoutube.cs:19, 23`、`YoutubeAuthentication.cs:222-228` | InnerException の無検証キャストと null 前提の GetType() で、本来の認証エラーを NRE が上書きすることがある。カスタム認証情報の使用有無でログ文言が逆 | パターンマッチで安全に判定。文言を実態に合わせる |

---

## 7. テスト基盤の問題（修正と並行して直す）

現行テストは48件で「追加／削除」と「サムネイル・画像のファイル処理」のみ。以下の領域は **テストゼロ** で、今回の修正対象そのものが無防備になっている。

- PublishAt スケジュール（UI 側 `PublishAtScheduleViewModel` は2,716行でリポジトリ最大。Schedule 本体の単体テストもない）
- アップロード実行（開始／停止／再開／UploadStats／後処理アクション）
- EventAggregator、自動化（Watcher／serializeandExecute）、Converter／Validator 群、電源まわり（PowerSaving／Shutdown）
- 既存テストの作り自体にも順序依存（`[Order]` 連番＋静的共有状態）、テスト間のグローバル状態（同一保存フォルダ、シングルトンのリフレクションリセット）の問題があり、並列化も個別実行もしづらい

**方針**: 新規修正には必ず対応テストを付ける（上記各 BUG の「検証」欄）。既存テストの作り直しは単独フェーズにせず、新規テストを独立実行可能な形で積み、古い順序依存テストは触った範囲から置換していく。

---

## 8. 実施順序（フェーズ案）と .NET 10 移行の位置づけ

前提の判断: **.NET 8 のサポート終了は 2026年11月10日**。.NET 10（LTS、2028年11月まで）への移行は小さな差分で済むため先に終わらせ、以後のバグ修正は .NET 10 上で検証する。11（STS）は終了時期が10とほぼ同じで得がないため対象外（2026-10-23 の検討で決定）。

| フェーズ | 内容 | 主な対象 | 完了条件 |
|---|---|---|---|
| Phase 0 | ベースライン確保：現行のビルド＋48テストが通る状態を記録。壊れた JSON のテスト資産を用意 | Test | ベースライン記録、資産追加 |
| Phase 1 | .NET 10 移行（TFM・パッケージ・インストーラの .NET 判定と案内文、Setup 内の net6.0 残骸パス修正）。バグ修正は混ぜない | 全 csproj、VidUp.Setup | ビルド＋48テスト通過、インストーラ判定の確認 | **実施済み（2026-10-23）**: 全プロジェクト 0 エラー、警告数は移行前と一致。テスト実行と画面・インストーラの実機確認は Windows 環境待ち（BASELINE.md §5 に記録） |
| Phase 2 | P0 の保存まわり：BUG-01（finally＋atomic化＋.bak）、BUG-13（ロック整備）、BUG-04（補正の安全化）、BUG-21/23（読込の堅牢化） | Json、Business | 並行保存ストレス＋壊れた JSON 読込テスト通過 | **実施済み（2026-10-23）**: BrokenJson 資産6件すべて読込成功、並行保存ストレス（300回・4ファイル混在）でエラー0＆全ファイル解析可能、書込失敗注入でもカウンタ回復と元ファイル無傷を確認。詳細は BASELINE.md §6 と BrokenJson_README の実測表。Windows 実機での既存48テスト実行は未確認のまま |
| Phase 3 | P0 のアップロードまわり：BUG-03（ストリームのハング）、BUG-05（再開照会）、BUG-06（Timer/resetEvent）、BUG-15（static 解消） | Youtube | モック応答テスト、ハングしないことのタイムアウト付きテスト | **完了（2026-10-23）**: BUG 単位で4コミット。応答分類を ResumePositionEvaluator に分離して単体テスト化、新規テスト14件、全61件が Wine 上で合格。詳細は BASELINE §8 |
| Phase 4 | スケジュール：BUG-07、BUG-11、BUG-12、BUG-22（特性テストを先行） | Business | ✅ 完了（2026-10-23、§9 参照。新規テスト22件、全83件合格） |
| Phase 5 | ライフサイクルとスレッド：BUG-02（省電力）、BUG-08（Watcher 終了処理）、BUG-18（Timer 破棄）、BUG-19（自動化の finally）、BUG-14（EventAggregator） | UI、Youtube | **完了（2026-10-24、BASELINE §10）**: 全テスト 89/89（Wine） |
| Phase 6 | 認証とキャンセル：BUG-10（refresh_token／リスナー）、BUG-09（トークン伝播＋resetEvent の本格置換：排他を SemaphoreSlim 化）、BUG-16（トークン辞書）、BUG-17（null 応答） | Youtube | **完了（2026-10-24、BASELINE §11）**: 全テスト 102/102（Wine）。手動ログイン確認のみ未実施（実アカウントが必要） |
| Phase 7 | 残り：BUG-20（async void の段階是正）、BUG-24〜32（P3 一括）、BUG-25 の正規化、BUG-28 の進捗計算 | 全体 | **完了（2026-10-24、BASELINE §12）**: 全テスト 126/126（Wine）。残る未確認は実機が必要な項目のみ（電源操作・実ログイン・UI 操作感）。全フェーズ完了 |

順序の理由: Phase 2・3 は「通常使用で実害が出る」順。Phase 4 はテスト整備が前提になる分だけ後ろ。Phase 5・6 は触る範囲が UI／認証に広く、影響確認に手動手順が要るため分けた。

## 9. 判断事項（4点すべて決定済み、2026-10-23）

1. ~~アップロード全体の同期プリミティブ（resetEvent 方式）をどうするか~~ **決定済み（2026-10-23）**: Phase 3 は `finally` 化の最小修正でデッドロック（BUG-06）だけを先に潰し、本格置換は Phase 6 で BUG-09 と合わせて行う。置換では「止める」を CancellationToken の伝播に、「統計更新の排他」を `SemaphoreSlim.WaitAsync(token)` に分離する。
2. ~~JSON の `.bak` 世代数と、読込フォールバックの警告の出し方~~ **決定済み（2026-10-23）**: `.bak` は1世代のみ。フォールバック発生時は Tracer だけでなく画面のステータス表示でも知らせる（BUG-01 の修正方針に反映済み）。
3. ~~修正を本家（Drexel2k/VidUp）へ PR として送る想定にするか~~ **決定済み（2026-10-23）**: 本家へは PR しない。本家は実質更新停止のため、fork（JamesBond000000-0/VidUp）内でのみ改善を進める。ただし履歴を追えるよう、コミットはフェーズ／対象 BUG ごとに小さく分け、コミットメッセージに理由を残す（§0 原則1に反映済み）。
4. ~~Phase 1（.NET 10）と Phase 2 の前後を入れ替えるか~~ **決定済み（2026-10-23）**: 本書の順序どおり Phase 1（.NET 10 移行）を先行させる。サポート終了（2026年11月10日）という動かせない期限を先に解消し、以後すべての修正を .NET 10 上で検証する。

（以上）
