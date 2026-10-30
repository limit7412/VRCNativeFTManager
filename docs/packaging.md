# 配布パッケージ

リリースへ添付するzipは `.github/scripts/build-packages.sh` が作る。
release.ymlとprerelease.ymlの両方がこれを呼ぶため、安定版とプレリリースで中身が食い違うことはない。

| 生成物 | 中身 | 用途 |
| ---- | ---- | ---- |
| `com.qazx7412.kx-vrc-native-ft-manager-<版>.zip` | `package.json` `LICENSE` `Runtime/` `Editor/`（`.meta` を含む） | VCC/ALCOM |
| `VRCNativeFTManager_<版>.zip` | このパッケージのunitypackageと、ARKit BlendShape Generatorのunitypackage | booth |

unitypackageは `.github/scripts/booth_package.py` がUnityを使わずに組み立てる。
unitypackageの実体はgzip tarで、アセット1件につきGUIDを名前とするディレクトリを持ち、その中に中身とメタと取り込み先のパスを並べた形式である。
リリース経路へUnityライセンスを持ち込まずに済ませるため、Pythonで直接組み立てている。

## Generatorの同梱

ARKit BlendShape Generatorは必須の依存で、Generatorが無いとEditorアセンブリがコンパイルできない。
VCC/ALCOMは `vpmDependencies` から入れるが、booth版は依存解決を通らないため、booth用zipにGeneratorのunitypackageを並べる。

Generatorは `.github/scripts/fetch_generator.py` が、Generatorリポジトリのリリースに添付されたbooth用zip（`VRCARKitBlendShapeGenerator_<版>.zip`）から取り出す。
選ぶのは、`package.json` にあるGeneratorの下限を満たす版のうち最新のもの。
このパッケージがプレリリースならGeneratorのプレリリースも候補にし、安定版なら安定版を優先する（下限を満たす安定版が無いときだけプレリリースに倒す）。
GitHubが返すダイジェスト（sha256）と照合してから使う。

同梱するGeneratorは、このパッケージのリリースを作った時点で決まる。
Generatorを新しくしただけではこのパッケージのプレリリースは作られない（prerelease.ymlの判定はこのリポジトリの変更だけを見る）。
新しいGeneratorを同梱し直したいときは、prerelease.ymlを `workflow_dispatch` から実行する。

自己更新（`Editor/Infra/SelfUpdater.cs`）は、zipの中から `VRCNativeFTManager_` で始まるunitypackageだけを取り込み、Generatorは取り込まない。
`booth_package.py` は、同梱する依存の名前がこの接頭辞で始まらないことを確かめる。

## booth用zipの条件

booth用zipの直下に `package.json` を置かないことが条件になる。
VPMリスティングの生成はリリースのzipをすべて舐めて直下の `package.json` を読むため、置いてしまうとbooth用の配布物がリスティングへ混ざる。
`packaging` ジョブ（[ci.md](ci.md)）がこれを検証する。

同梱対象のGUIDは `.meta` としてリポジトリで固定している。
VPM用zipも `.meta` を同梱する。外すと導入のたびにGUIDが振り直され、更新した瞬間に利用者のアバターからコンポーネントの参照が切れる。
**同梱対象の `.meta` のGUIDは変更しないこと。** ファイルの追加時に新しいGUIDを起こすのは問題ない。
アセットを意図して配布物から外すときは、`booth_package.py` の `RETIRED_GUIDS` にGUIDを載せる。載せないと `packaging` ジョブが消えたGUIDとして止める。0.1.0-test6でプレハブを外したときに、この一覧を作った。
