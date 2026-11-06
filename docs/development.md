# 開発環境とテストの実行

テストは `Tests/Editor` にEditModeテストとして置いている。
Unityに依存しない判定は `Editor/Domain` に集め、テストではそこを直接検証する。

`Editor/` `Runtime/` を変更したときは、PRを出す前に手元で実行する。
CIでも実行されるが（[ci.md](ci.md)）、Unityライセンスのsecretが未設定のリポジトリやフォークではスキップされる。

1. このリポジトリをUnityプロジェクトの `Packages/` 以下へ配置する（VCC/ALCOM経由で導入したものは書き換えられないため、開発時はクローンを直接置く）
2. `Packages/manifest.json` の `testables` にパッケージ名を追加する

    ```json
    {
      "dependencies": { },
      "testables": [ "com.qazx7412.kx-vrc-native-ft-manager" ]
    }
    ```

3. Unityメニュー > Window > General > Test Runner を開く
4. EditModeタブで `NativeFTManager.Editor.Tests` を実行する

`testables` へ追加しないとテストアセンブリがコンパイルされず、Test Runnerの一覧にも現れない。
テストは配布物には含まない（配布物の中身は [packaging.md](packaging.md) に書いている）。

## アセンブリの構成

| アセンブリ | 場所 | コンパイルされる条件 |
| ---- | ---- | ---- |
| `NativeFTManager.Runtime` | `Runtime/` | 常に |
| `NativeFTManager.Editor` | `Editor/` | 常に（エディタのみ） |
| `NativeFTManager.Editor.Tests` | `Tests/Editor/` | `testables` に追加したとき |

任意依存の型に触るコードは、対応する連携用アセンブリにだけ置く。
本体のアセンブリから参照すると、その依存が無いプロジェクトでコンパイルが通らなくなる。
連携用アセンブリの判定はasmdefの `versionDefines` と `defineConstraints` で行う。
Avatar Optimizerの型には触らないため、AAO対策の処理は持たない。
AAOは1.9.21でネイティブFTに対応し、SetとParameter Behaviorsの対象シェイプを自分で保護する。
本体のasmdefでは `versionDefines` でAAOの版（`KX_AAO_NATIVE_FT`、1.9.21以降）とSDKの版（`KX_VRC_SDK_AAO_FT`、AAO 1.9.21が対応を有効にする3.10.6-beta.4以降）を判定し、古い組み合わせのときに警告（W-14、W-15）を出すだけにしている。
かつてAAO対策用に置いていた空のアセンブリ `NativeFTManager.Editor.AAOCompat`（`Editor/AAOCompat/`）は取り除き、そのGUIDは `booth_package.py` の `RETIRED_GUIDS` に載せた。

ARKit BlendShape Generatorは必須の依存だが、`NativeFTManager.Editor` はGeneratorのアセンブリを参照しない。
booth版はVPMの依存解決を通らず、同梱したGeneratorのunitypackageを取り込み忘れることがある。asmdefで参照すると、そのときEditorアセンブリ全体がコンパイルできず、警告も出せない。
Generatorの型には `Editor/Infra/GeneratorAccess.cs` だけがリフレクションで触る。型やフィールドが見つからなければ `GeneratorAccess.IsInstalled` がfalseになり、W-20を出してアバターにGeneratorが無いのと同じに振る舞う。
asmdefの `versionDefines` は `Packages/` のパッケージにしか効かず、`Assets/` に入るbooth版のGeneratorを判定できないので使わない。
Set名の定数 `FaceTrackingSetNames.Generator` は `GeneratorAccess.DefaultSetName` に複製してある。Generatorを入れた構成のテスト（`GeneratorAccessTests`）が一致を確かめる。
アバターにGeneratorのコンポーネントが無くSetも無いときは、Generator 0.2.2以降が公開する `ARKitBlendShapeGenerator.FaceTrackingSetMapping` を同じくリフレクションで呼び、メッシュのシェイプ名からSetを作る（`Editor/Infra/ShapeMappedSetReader.cs`、仕様書10.1のF-20）。`GetKeyNames()` の並びが `FaceTrackingGroupTable` と一致しないときは使わない。
「ARKitをUEに変換」がオンなら、Setを作る前に `ARKitBlendShapeGenerator.UnifiedExpressionsConversion.ConvertArkitShapes(SkinnedMeshRenderer)` を同じくリフレクションで呼び、同じメッシュへUE名のシェイプキーを書き足す（`GeneratorAccess.ConvertToUnifiedExpressions`）。変換はGenerator 0.2.3から公開されており、それより古い版では何もしない。
アバターにGeneratorのコンポーネントがあるときは、この変換を呼ばない。代わりにPrepareで、Generatorのフィールド `outputNaming` がARKitなら「両方」（`Both`）へ書き換える（`GeneratorAccess.EnableUnifiedExpressionsOutput`）。GeneratorはTransforming PhaseでManagerのApplyより先に動き、出力名がARKit以外なら元からあるARKit名のシェイプキーもUE名へ変換し、UE名を優先してSetを書くためである。
CIの `EditMode tests` はGeneratorを入れた構成と入れない構成の両方で回す。テストのasmdefは `versionDefines` で `KX_TEST_GENERATOR_INSTALLED` を立て、構成ごとに検証を切り替える。
かつてGeneratorは任意依存で、連携の処理はGeneratorが入っているときだけコンパイルする `NativeFTManager.Editor.GeneratorCompat` に置いていた。本体へ統合したあと、そのGUIDは `booth_package.py` の `RETIRED_GUIDS` に載せた。

Generatorの版の下限は `package.json` の `vpmDependencies` にある。
今の下限は、Generatorの安定版の0.2.3である。連携に使う型は、Set名の定数（`FaceTrackingSetNames`）が0.2.1-test3から、対応表（`FaceTrackingSetMapping`）が0.2.2から、UE変換（`UnifiedExpressionsConversion`）が0.2.3から含まれている。
booth版は依存解決を通らず古いGeneratorが残ることがあるので、下限より古い版でも動くよう、型が無ければその機能だけを止める。
下限を変えるときは、テストのワークフローの `GENERATOR_VERSION`（[ci.md](ci.md)）も揃える。

## EXメニューのアイコン

EXメニューの既定アイコンは `Runtime/Icons/` の PNG（256x256、透過背景、白の線画）で、元の SVG は `docs/icons~/src/` にある。図柄を変えるときは SVG を直し、リポジトリのルートで `node docs/icons~/render.js` を実行して PNG を書き出す（Playwright の Chromium を使う）。PNG の `.meta` は書き換えない。GUID は `Editor/Infra/MenuIconAssets.cs` が持ち、利用者の Menu Item がこの GUID で画像を参照している。アイコンを足すときは、`.meta` を作って GUID を `MenuIconAssets` と `MenuIcon` に加える。

## 実機確認用の道具

issue #2 の実機確認に使う道具は `Tools~/` に置いている（[Tools~/README.md](../Tools~/README.md)）。
フォルダ名の末尾の `~` で Unity の取り込みから外しており、配布物にも入らない。

- `Tools~/ft-sender/`: VRCFT と同じ形式で `/tracking/face/v1` に固定値の顔データを送る Python スクリプト
- `Tools~/test-write-layer/`: FT と同じシェイプへ固定値を書き続ける FX レイヤーを置くエディタ拡張。使うときは `Assets/` の下へコピーする
