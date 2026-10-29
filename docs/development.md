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
| `NativeFTManager.Editor.GeneratorCompat` | `Editor/GeneratorCompat/` | ARKit BlendShape Generatorが入っているとき（define `KX_ARKIT_GENERATOR`） |
| `NativeFTManager.Editor.AAOCompat` | `Editor/AAOCompat/` | Avatar Optimizer 1.6.0以上2.0.0未満が入っているとき（define `KX_AAO_API`） |
| `NativeFTManager.Editor.Tests` | `Tests/Editor/` | `testables` に追加したとき |

任意依存の型に触るコードは、対応する連携用アセンブリにだけ置く。
本体のアセンブリから参照すると、その依存が無いプロジェクトでコンパイルが通らなくなる。
連携用アセンブリの判定はasmdefの `versionDefines` と `defineConstraints` で行う。
GeneratorCompatの版の範囲は、Generatorが連携に必要な定数（`FaceTrackingSetNames`）を公開する版（仕様書16章のP1）のリリースで確定する。今の `[0.2.1,1.0.0)` は、P1を含む次のリリースを0.2.1と見込んだ仮の値である。
