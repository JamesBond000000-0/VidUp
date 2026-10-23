# 壊れた JSON テスト資産（Phase 0 で用意、Phase 2 / 4 の読込テストで使用）

各フォルダは正常な設定一式（`BasicTests_RemoveUploadFinishedWithoutTemplate` のコピー）に
**1種類だけ**意図的な破損を入れたもの。現行コードではいずれも読込失敗／例外／ハングの
いずれかになる想定で、修正フェーズで「読めるものは読める」ことを検証するために使う。

| フォルダ | 破損内容 | 対象 BUG | 修正後の期待動作 |
|---|---|---|---|
| BrokenJson_UnknownCategoryId | テンプレートとアップロードの `category` に未知の ID `9999` | BUG-21 | 読込成功。カテゴリは既定（null）にフォールバックし ReSerialize フラグが立つ |
| BrokenJson_TemplateMismatch | 第2テンプレートの `uploads` に、第1テンプレートを指すアップロードを含めた（保持テンプレートと `template` フィールドの不一致） | BUG-04 | 読込成功。列挙中の変更例外を出さず、不整合が補正される |
| BrokenJson_MalformedGuids | アップロードの `guid` が `"not-a-guid"`、別のアップロードの `youtubeAccount` が空文字 | BUG-21 | 壊れたレコードだけをスキップ／代替アカウントへ寄せ、他は読める |
| BrokenJson_ExplicitNulls | アップロードの `tags` と `statusInformation` が明示 null、テンプレートの `uploads` が null | BUG-23 | 読込成功。null は初期値に正規化される |
| BrokenJson_TemplatesNull | `templatelist.json` が `{"templates": null}` | BUG-23 | 読込成功（空のテンプレート一覧として扱う） |
| BrokenJson_MonthlyRelativeNoCombinations | 月次・相対方式のスケジュールで `monthlyMonthRelativeBasedCombinations` 項目自体が欠落 | BUG-12 / BUG-22 | 次回日時計算で StackOverflow にならず、「次の日時なし」として扱われる |
| BrokenJson_TruncatedUploads | `uploads.json` が途中で切断されている（BUG-01 の半端な書き込みの再現） | BUG-01 | `.bak` がある場合はフォールバック、ない場合はクラッシュせずエラーとして扱われる |

注意: BrokenJson_TruncatedUploads の uploads.json だけは意図的に JSON として不正。
それ以外のファイルはすべて JSON としては正しい（意味的に壊れている、が区別点）。

## Phase 2 での実測結果（2026-10-23）

検証ハーネス（VidUp.Json を直接実行し、各資産で `JsonDeserializationContent.Deserialize` を回す）で実測。

| フォルダ | 修正前（実測） | 修正後（実測） |
|---|---|---|
| BrokenJson_UnknownCategoryId | InvalidOperationException（Sequence contains no elements）で読込失敗 | 読込成功。カテゴリは null にフォールバックし AllUploads/TemplateList の再シリアライズフラグが立つ |
| BrokenJson_TemplateMismatch | InvalidOperationException（Collection was modified）で読込失敗 | 読込成功。不整合が補正され、状態が収束する（First に1件・template=First、Second は空） |
| BrokenJson_MalformedGuids | JsonSerializationException（Guid 変換）で読込失敗 | 読込成功。壊れた GUID のアップロードだけ除去、空アカウントは代替アカウントへ寄せ、正常レコードは無事 |
| BrokenJson_ExplicitNulls | NullReferenceException で読込失敗 | 読込成功。tags/statusInformation/uploads の null は空に正規化 |
| BrokenJson_TemplatesNull | NullReferenceException で読込失敗 | 読込成功（テンプレート0件として扱う） |
| BrokenJson_TruncatedUploads | JsonSerializationException で読込失敗 | .bak があればフォールバック回復。無い場合は uploads 空で起動継続（部分的に読めたレコードの救済はしない） |

正常な設定（BasicTests_RemoveUploadFinishedWithoutTemplate）の読込結果は修正前後で同一であることも確認済み。
