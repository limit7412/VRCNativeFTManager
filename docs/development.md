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
0.1.0-test11までは、AAO対策用に空のアセンブリ `NativeFTManager.Editor.AAOCompat`（`Editor/AAOCompat/`）を残していた。0.1.0-test12で取り除き、そのGUIDは `booth_package.py` の `RETIRED_GUIDS` に載せた。

ARKit BlendShape Generatorは必須の依存で、`NativeFTManager.Editor` がGeneratorの `ARKitBlendShapeGenerator.Runtime` を直接参照する。
Generatorの型に触るのは `Editor/Infra/GeneratorAccess.cs` だけにしている。
0.1.0-test9までは任意依存で、Generatorが入っているときだけコンパイルする `NativeFTManager.Editor.GeneratorCompat` に置いていた。0.1.0-test10で本体へ統合し、そのGUIDは `booth_package.py` の `RETIRED_GUIDS` に載せた。

Generatorの版の下限は `package.json` の `vpmDependencies` にある。
今の下限は、連携に必要な定数（`FaceTrackingSetNames`）を最初に含んだプレリリースの0.2.1-test3である。
Generatorの0.2.1を正式にリリースしたら、下限を `0.2.1` に上げてよい。上げるときはテストのワークフローの `GENERATOR_VERSION`（[ci.md](ci.md)）も揃える。
