#!/usr/bin/env bash
# リリースへ添付するパッケージを作る。
#
# release.ymlとprerelease.ymlの両方から呼ばれる。
# 以前は同じzip生成ステップが両方に書かれており、片方だけ変更するとプレリリースと
# 安定版で中身の違うパッケージができる状態だった。ここへ集約してその余地を無くす。
#
# 出力は2つで、どちらもリリースの`files: "*.zip"`がそのまま拾う。
#   com.qazx7412.kx-vrc-native-ft-manager-<version>.zip  VCC/ALCOM向け
#   VRCNativeFTManager_<version>.zip                    booth向け(このパッケージとGeneratorのunitypackage)
#
# booth向けの組み立てでは、必須の依存であるKx VRC ARKit BlendShape Generatorのリリースから
# booth用zipを取得するため、ネットワークとGitHub APIへの到達が要る(GH_TOKENがあれば使う)
set -euo pipefail

VERSION="${1:?バージョンを指定すること}"
OUTPUT_DIR="${2:-.}"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

cd "$REPO_ROOT"
mkdir -p "$OUTPUT_DIR"
OUTPUT_DIR="$(cd "$OUTPUT_DIR" && pwd)"

PACKAGE_NAME="com.qazx7412.kx-vrc-native-ft-manager"

# package.jsonのversionはリリース時にタグから上書きする運用のため、
# リポジトリ上の値ではなく渡されたバージョンを書き込む
jq --arg version "$VERSION" '.version = $version' package.json > package.json.tmp
mv package.json.tmp package.json

# VPM用。
#
# `.meta`も含める。Unityはアセットへの参照を`.meta`のGUIDで解決するため、
# 配布物から外すと導入のたびにGUIDが振り直され、更新後のアバターで
# コンポーネントがMissing Scriptになる。
# ディレクトリ自身の`.meta`はパッケージ直下にあるので個別に指定する。
VPM_ZIP="$OUTPUT_DIR/${PACKAGE_NAME}-${VERSION}.zip"
rm -f "$VPM_ZIP"
zip -q -r "$VPM_ZIP" \
  package.json \
  package.json.meta \
  LICENSE \
  LICENSE.meta \
  Runtime/ \
  Runtime.meta \
  Editor/ \
  Editor.meta

echo "Created: $(basename "$VPM_ZIP")"
ls -la "$VPM_ZIP"

# booth用。中身はこのパッケージのunitypackageと、同梱するGeneratorのunitypackageで、
# zipの直下に`package.json`を置かない。置くとVPMリスティングの生成がこれをパッケージとして拾ってしまう。
#
# Generatorは、package.jsonの下限を満たす最新のリリースから取る(選び方はfetch_generator.py)。
# 取り出したunitypackageは出力先に置かない。`files: "*.zip"`は拾わないが、
# 作業ディレクトリに残すと次の組み立てに古い版が紛れるため、一時ディレクトリへ置く
BUNDLE_DIR="$(mktemp -d)"
trap 'rm -rf "$BUNDLE_DIR"' EXIT
GENERATOR_UNITYPACKAGE="$(python3 "$SCRIPT_DIR/fetch_generator.py" --version "$VERSION" --output-dir "$BUNDLE_DIR")"

python3 "$SCRIPT_DIR/booth_package.py" build --version "$VERSION" --output-dir "$OUTPUT_DIR" \
  --bundle "$GENERATOR_UNITYPACKAGE"
ls -la "$OUTPUT_DIR/VRCNativeFTManager_${VERSION}.zip"
