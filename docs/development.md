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
| `NativeFTManager.Editor.AAOCompat` | `Editor/AAOCompat/` | Avatar Optimizer 1.6.0以上1.9.21-beta.1未満が入っているとき（define `KX_AAO_API`）。中身は無く、GUIDを保つためだけに残している |
| `NativeFTManager.Editor.Tests` | `Tests/Editor/` | `testables` に追加したとき |

任意依存の型に触るコードは、対応する連携用アセンブリにだけ置く。
本体のアセンブリから参照すると、その依存が無いプロジェクトでコンパイルが通らなくなる。
連携用アセンブリの判定はasmdefの `versionDefines` と `defineConstraints` で行う。
Avatar Optimizerの型には触らないため、AAO対策の処理は持たない。
`Editor/AAOCompat/` は0.1.0-test1で配ったGUIDを保つためだけに残した空のアセンブリで、対応前のAAO（`[1.6.0,1.9.21-beta.1)`）でしかコンパイルされない。
AAOは1.9.21-beta.1でネイティブFTに対応し、SetとParameter Behaviorsの対象シェイプを自分で保護する。
本体のasmdefでは `versionDefines` でAAOの版（`KX_AAO_NATIVE_FT`）とSDKの版（`KX_VRC_SDK_AAO_FT`）を判定し、古い組み合わせのときに警告（W-14、W-15）を出すだけにしている。

GeneratorCompatの版の範囲は、Generatorが連携に必要な定数（`FaceTrackingSetNames`）を公開する版（仕様書16章のP1）のリリースで確定する。
今の下限は、この定数を最初に含んだプレリリースの0.2.1-test3である。
版の比較ではプレリリースが正式版より小さいため、下限を `0.2.1` にすると0.2.1-testNが範囲から外れ、GeneratorCompatがコンパイルされない。
0.2.1を正式にリリースしたら、下限を `0.2.1` に上げてよい。
