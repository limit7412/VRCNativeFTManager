#!/usr/bin/env python3
"""booth用zipへ同梱するKx VRC ARKit BlendShape Generatorのunitypackageを取得する。

GeneratorはこのパッケージのVPM依存であり、VCC/ALCOMなら自動で入る。
booth版はunitypackageで入れるため依存が解決されず、Generatorが無いとこのパッケージの
Editorアセンブリがコンパイルできない。そこでbooth用zipにGeneratorのunitypackageを並べる。

取得元はGeneratorリポジトリのリリースに添付されたbooth用zipで、その中のunitypackageを取り出す。
選ぶ版は`package.json`の`vpmDependencies`にある下限を満たすもののうち最新のもの。
このパッケージがプレリリースならGeneratorのプレリリースも候補にする。
安定版なら安定版を優先し、下限を満たす安定版が無いときだけプレリリースに倒す。
"""

import argparse
import hashlib
import io
import json
import os
import pathlib
import re
import sys
import urllib.request
import zipfile

GENERATOR_REPO = "limit7412/VRCARKitBlendShapeGenerator"
GENERATOR_PACKAGE = "com.qazx7412.kx-vrc-arkit-blendshape-generator"
GENERATOR_BOOTH_BASENAME = "VRCARKitBlendShapeGenerator"

SEMVER = re.compile(r"^v?(\d+)\.(\d+)\.(\d+)(?:-([0-9A-Za-z.-]+))?$")


class FetchError(Exception):
    pass


def parse_version(text):
    match = SEMVER.match(text.strip())
    if not match:
        raise FetchError(f"版として読めない: {text}")
    major, minor, patch, pre = match.groups()
    return (int(major), int(minor), int(patch)), pre


def version_key(text):
    """semverの順序で比べるためのキー。プレリリースは同じ版の安定版より前に来る"""
    core, pre = parse_version(text)
    if pre is None:
        return core, 1, ()

    identifiers = []
    for part in pre.split("."):
        # 数字だけの識別子は数として、他は文字列として比べ、数字の方を前に置く。
        # `test10`のように末尾が数字の識別子は、文字列のままだと`test3`より前に来るため、
        # 末尾の数字を数として比べる。リリースのタグは`X.Y.Z-testN`の形で積まれている
        if part.isdigit():
            identifiers.append((0, int(part), "", 0))
            continue
        match = re.fullmatch(r"(.*?)(\d+)", part)
        if match:
            identifiers.append((1, 0, match.group(1), int(match.group(2))))
        else:
            identifiers.append((1, 0, part, -1))
    return core, 0, tuple(identifiers)


def read_minimum(repo_root):
    manifest = json.loads((repo_root / "package.json").read_text(encoding="utf-8"))
    expression = manifest.get("vpmDependencies", {}).get(GENERATOR_PACKAGE)
    if not expression:
        raise FetchError(f"package.jsonのvpmDependenciesに{GENERATOR_PACKAGE}が無い")

    match = re.fullmatch(r"\s*>=\s*(\S+)\s*", expression)
    if not match:
        raise FetchError(f"Generatorの版の指定が`>=X.Y.Z`の形ではない: {expression}")
    return match.group(1)


def _request(url):
    request = urllib.request.Request(url, headers={"Accept": "application/vnd.github+json"})
    token = os.environ.get("GH_TOKEN") or os.environ.get("GITHUB_TOKEN")
    if token:
        request.add_header("Authorization", f"Bearer {token}")
    with urllib.request.urlopen(request, timeout=60) as response:
        return response.read()


def list_releases():
    releases = []
    page = 1
    while True:
        batch = json.loads(_request(
            f"https://api.github.com/repos/{GENERATOR_REPO}/releases?per_page=100&page={page}"))
        if not batch:
            return releases
        releases.extend(batch)
        page += 1


def select_release(releases, minimum, allow_prerelease):
    """下限を満たす最新の版を選ぶ。booth用zipが添付されていない版は候補にしない"""
    floor = version_key(minimum)
    candidates = []
    for release in releases:
        if release.get("draft"):
            continue
        tag = release.get("tag_name", "")
        try:
            key = version_key(tag)
        except FetchError:
            continue
        if key < floor:
            continue
        asset_name = f"{GENERATOR_BOOTH_BASENAME}_{tag.lstrip('v')}.zip"
        asset = next((a for a in release.get("assets", []) if a.get("name") == asset_name), None)
        if asset is None:
            continue
        candidates.append((key, bool(release.get("prerelease")), release, asset))

    stable = [c for c in candidates if not c[1]]
    pool = candidates if allow_prerelease or not stable else stable
    if not pool:
        raise FetchError(f"{minimum}以上でbooth用zipを持つGeneratorのリリースが無い")
    return max(pool, key=lambda c: c[0])


def extract_unitypackage(archive_bytes, tag):
    expected = f"{GENERATOR_BOOTH_BASENAME}_{tag.lstrip('v')}.unitypackage"
    with zipfile.ZipFile(io.BytesIO(archive_bytes)) as archive:
        names = archive.namelist()
        if expected not in names:
            raise FetchError(f"Generatorのbooth用zipに{expected}が無い: {names}")
        return expected, archive.read(expected)


def main(argv):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo-root", default=".")
    parser.add_argument("--version", required=True, help="このパッケージの版。プレリリースかどうかで候補を変える")
    parser.add_argument("--output-dir", required=True)
    args = parser.parse_args(argv)

    repo_root = pathlib.Path(args.repo_root).resolve()
    output_dir = pathlib.Path(args.output_dir).resolve()
    output_dir.mkdir(parents=True, exist_ok=True)

    try:
        _, own_pre = parse_version(args.version)
        minimum = read_minimum(repo_root)
        _, prerelease, release, asset = select_release(list_releases(), minimum, own_pre is not None)
        tag = release["tag_name"]

        payload = _request(asset["browser_download_url"])
        digest = asset.get("digest") or ""
        if digest.startswith("sha256:"):
            actual = hashlib.sha256(payload).hexdigest()
            if actual != digest[len("sha256:"):].lower():
                raise FetchError(f"{asset['name']}のダイジェストが一致しない: {actual}")

        name, unitypackage = extract_unitypackage(payload, tag)
    except FetchError as error:
        print(f"NG: {error}", file=sys.stderr)
        return 1

    path = output_dir / name
    path.write_bytes(unitypackage)
    kind = "プレリリース" if prerelease else "安定版"
    print(f"Generator {tag}（{kind}、下限 {minimum}）: {path}", file=sys.stderr)
    # 標準出力には取り出したファイルのパスだけを出す。build-packages.shが受け取る
    print(path)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
