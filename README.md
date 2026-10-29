# Kx VRC NativeFT Manager

VRChatのネイティブFace Tracking向けに、Jerry's Templates相当のトグルと表情補正をEXメニューから操作できるようにするNDMFプラグインです。
プレハブをアバターへ置くだけで使え、[Kx VRC ARKit BlendShape Generator](https://github.com/limit7412/VRCARKitBlendShapeGenerator)に連携します。
FaceEmoの抑制も、今後のバージョンで本製品に持たせます。[Kx VRC FE-JsT Bridge](https://github.com/limit7412/VRCFE-JsTBridge)とは連携せず、同じアバターでの併用もしません。

> [!WARNING]
> **開発中です。** 現在のバージョンで動くのは、Blendshapes Setのグループ分割、FT全体・目・口・瞳孔・ビゼームのトグル、表情補正（エミュレーション、制限、Eye Sync）です。FaceEmoの抑制はまだありません。
> トグルが使うState Behaviorの型と、表情補正の切り替えに使う空のSet（ゲート）が効くかは、VRChat SDKのベータ版で確かめている途中です。State Behaviorの型が見つからないときはビルドで警告（KxNFTM-E-05）が出て、トグルだけが効きません。
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
2. インスペクタでプリセットを選ぶ。UE（既定）とARKitはJerry'sと同じトグルと表情補正を持ち、Minimalは表情補正を作らずトグルも3つだけにする。トグルや補正の規則を個別に変えるとCustomになる
3. アップロードする。EXメニューの「Face Tracking」から、Face Tracking・Eye Tracking・Lip Tracking・Pupil・Visemesを切り替える。表情補正の切り替え（Emulation・Limits・Eye Sync）は、その下の「Settings」にある

Jerry's Face Tracking Templatesは同じアバターに置けません。残っているとインスペクタとビルドでエラー（KxNFTM-E-04）が出ます。

### ビルド時に行うこと

- DescriptorのBlendshapes Setを、トラッキングキーの種類でEye / EyeClosed / Pupil / Brow / Lip / Otherに分ける（`KxNFTM/Eye` など）。元のSetは消え、キーとシェイプの対応の総数は変わらない
- メニューに出すトグルごとに同期Boolのパラメーターとメニューを作り、FXのレイヤーでSetの重みを0と1に切り替える。パラメーターは `KxNFTM/FaceTracking`、`KxNFTM/EyeTracking`、`KxNFTM/LipTracking`、`KxNFTM/Pupil`、`KxNFTM/Visemes`、`KxNFTM/Emulation`、`KxNFTM/Limits`、`KxNFTM/EyeSync` で、UEプリセットなら計8bit
- Visemesを切ると、FXのTracking ControlでMouth & JawをAnimationにしてビゼームを止める
- 表情補正を、DescriptorのParameter Behaviors（Custom式）として足す。係数はJerry's 7.0.5のFXから移した（出典は[THIRD_PARTY_NOTICES](Editor/THIRD_PARTY_NOTICES.md)）
  - エミュレーション: 笑うと頬と口角のくぼみを上げる、口から眉を動かす、見開くと瞳孔を縮めるなど。追跡した値を下回らない下限（Limit Min）として書く
  - 制限: 唇をすぼめると口の上下を抑えるなど、同時に動かすと崩れる組み合わせの上限（Limit Max）を書く
  - Eye Sync: 左右のまばたきを平均して両目へ書く
  - 補正の切り替えには、対応を持たない空のSet（`KxNFTM/Gate/...`）の重みを使う。規則が読み書きするキーの側のトグル（目・瞳孔・口）を切ったときも、その規則は止まる
  - 対象のシェイプがSetに無い規則は作らない（KxNFTM-I-01）。Custom式の命令数がアバター全体の上限512を超えるときは、優先度の低い規則から落とす（KxNFTM-W-12）
- DescriptorのFace Trackingを有効にし、Use All Tracking Keysを有効にする（インスペクタで変更しないこともできる）

インスペクタの「見積もり」には、分割で作るSet、Parameter Behaviors、Custom式の命令数、Face Tracking Operations、同期bit、FXのレイヤーの数が出ます。
ARKit版でGeneratorのSetを使う場合、そのSetはビルド中にしか無いので、見積もりに入りません。

### Jerry'sからの移行で変わること

| 項目 | Jerry's 7.0.5 | 本製品 |
| --- | --- | --- |
| 入力 | VRCFTのOSC（`FT/v2/*`） | VRChatのネイティブFace Tracking |
| 瞳孔の既定 | 無効 | 有効 |
| エミュレーションの合成 | 追跡した値に足す | 追跡した値を下限にする |
| 配線から外れている制限の木（Limit Mouth Closed (JawOpen) など3つ） | 効いていない | 移していない |
| スムージング | あり | なし（VRChat側の補間に任せる） |

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

表情補正の係数はJerry's Face Tracking Templates（MIT License、Copyright (c) 2023 Adjerry91）から移しています。許諾表示は[THIRD_PARTY_NOTICES](Editor/THIRD_PARTY_NOTICES.md)にあります。
