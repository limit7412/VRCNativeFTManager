# Kx VRC NativeFT Manager

VRChatのネイティブFace Tracking向けに、Jerry's Templates相当のトグルと表情補正をEXメニューから操作できるようにするNDMFプラグインです。
プレハブをアバターへ置くだけで使え、[Kx VRC ARKit BlendShape Generator](https://github.com/limit7412/VRCARKitBlendShapeGenerator)と[Kx VRC FE-JsT Bridge](https://github.com/limit7412/VRCFE-JsTBridge)に連携します。

> [!WARNING]
> **開発中です。** 現在のバージョンにはパッケージの骨格（ビルドへの組み込み、更新の確認、配布の仕組み）だけが入っており、トグルや表情補正はまだ動きません。
> ネイティブFace TrackingはVRChat SDKのベータ版の機能です。SDKが正式版になるまでは実験的な扱いとします。

## 必要環境

- Unity 2022.3
- VRChat SDK Avatars 3.10.6-beta.2 以降（VCC/ALCOMでプレリリースの表示を有効にして導入する）
- NDMF 1.6.0 以降
- Modular Avatar 1.12.0 以降

次のパッケージは依存に含めていません。入っているときだけ連携が有効になります。

- Kx VRC ARKit BlendShape Generator
- Avatar Optimizer 1.6.0 以上 2.0.0 未満

## インストール

1. [VPMリポジトリ](https://limit7412.github.io/vcc-vpm/)をVCC/ALCOMへ追加する
2. プロジェクトへ「Kx VRC NativeFT Manager」を追加する

zipからの手動インストールとbooth版（unitypackage）の手順、および更新の通知と自己更新の仕組みは[インストールと更新](Documentation~/install.md)にまとめています。

## 使い方

機能の実装とともに書きます。

## ドキュメント

- [インストールと更新](Documentation~/install.md)
- [開発環境とテストの実行](docs/development.md)
- [CIでのテスト実行](docs/ci.md)
- [配布パッケージ](docs/packaging.md)

## 仕様

仕様と全体計画は [issue #1](https://github.com/limit7412/VRCNativeFTManager/issues/1) にまとめています。

## ライセンス

[MIT License](LICENSE)
