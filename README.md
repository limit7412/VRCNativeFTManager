# Kx VRC NativeFT Manager

VRChatのネイティブFace Tracking向けに、Jerry's Templates相当のトグルと表情補正をEXメニューから操作できるようにするNDMFプラグインです。
プレハブをアバターへ置くだけで使え、[Kx VRC ARKit BlendShape Generator](https://github.com/limit7412/VRCARKitBlendShapeGenerator)に連携します。
FaceEmoの抑制も、今後のバージョンで本製品に持たせます。[Kx VRC FE-JsT Bridge](https://github.com/limit7412/VRCFE-JsTBridge)とは連携せず、同じアバターでの併用もしません。

> [!WARNING]
> **開発中です。** 現在のバージョンで動くのは、Blendshapes Setのグループ分割と、FT全体・目・口のトグルです。表情補正（エミュレーション、制限、Eye Sync）、瞳孔とビゼームのトグル、FaceEmoの抑制はまだありません。
> トグルが使うState Behaviorの型は、VRChat SDKのベータ版で確かめている途中です。見つからないときはビルドで警告（KxNFTM-E-05）が出て、トグルだけが効きません。
> ネイティブFace TrackingはVRChat SDKのベータ版の機能です。SDKが正式版になるまでは実験的な扱いとします。

## 必要環境

- Unity 2022.3
- VRChat SDK Avatars 3.10.6-beta.2 以降（VCC/ALCOMでプレリリースの表示を有効にして導入する）
- NDMF 1.6.0 以降
- Modular Avatar 1.12.0 以降

次のパッケージは依存に含めていません。入っているときだけ連携が有効になります。

- Kx VRC ARKit BlendShape Generator
- Avatar Optimizer 1.9.21-beta.1 以降（併用する場合）

Avatar Optimizerは1.9.21-beta.1でネイティブFTに対応し、Blendshapes SetとParameter Behaviorsが使うシェイプを最適化から守るようになりました。
本製品はこの対応を前提にしており、独自のAvatar Optimizer対策は持ちません。
Avatar Optimizerの対応が効くのはVRChat SDK 3.10.6-beta.3以降なので、併用する場合はSDKもこの版以降にしてください。
古い組み合わせでは、ビルド時に警告（W-14、W-15）を出します。

## インストール

1. [VPMリポジトリ](https://limit7412.github.io/vcc-vpm/)をVCC/ALCOMへ追加する
2. プロジェクトへ「Kx VRC NativeFT Manager」を追加する

zipからの手動インストールとbooth版（unitypackage）の手順、および更新の通知と自己更新の仕組みは[インストールと更新](Documentation~/install.md)にまとめています。

## 使い方

1. `Packages/Kx VRC NativeFT Manager/Runtime/Prefabs/` のプレハブをアバター直下に置く
   - 汎用版（`Kx NativeFT Manager`）: DescriptorのBlendshapes Setを、オートフィルか手入力で用意してあるアバター向け
   - ARKit版（`Kx NativeFT Manager (ARKit)`）: Kx VRC ARKit BlendShape Generatorを使うアバター向け。GeneratorのSet書き込みを、ビルド中だけ有効にする
2. アップロードする。EXメニューの「Face Tracking」から、Face Tracking・Eye Tracking・Lip Trackingを切り替える

Jerry's Face Tracking Templatesは同じアバターに置けません。残っているとインスペクタとビルドでエラー（KxNFTM-E-04）が出ます。

### ビルド時に行うこと

- DescriptorのBlendshapes Setを、トラッキングキーの種類でEye / EyeClosed / Pupil / Brow / Lip / Otherに分ける（`KxNFTM/Eye` など）。元のSetは消え、キーとシェイプの対応の総数は変わらない
- トグルごとに同期Boolのパラメーター（`KxNFTM/FaceTracking`、`KxNFTM/EyeTracking`、`KxNFTM/LipTracking`、計3bit）とメニューを作り、FXのレイヤーでSetの重みを0と1に切り替える
- DescriptorのFace Trackingを有効にし、Use All Tracking Keysを有効にする（インスペクタで変更しないこともできる）

パラメーターの接頭辞 `KxNFTM/` は仮の名前で、1.0までに確定します。

## ドキュメント

- [インストールと更新](Documentation~/install.md)
- [開発環境とテストの実行](docs/development.md)
- [CIでのテスト実行](docs/ci.md)
- [配布パッケージ](docs/packaging.md)

## 仕様

仕様と全体計画は [issue #1](https://github.com/limit7412/VRCNativeFTManager/issues/1) にまとめています。

## ライセンス

[MIT License](LICENSE)
