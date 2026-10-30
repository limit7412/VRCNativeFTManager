# 必要環境と併用するパッケージ

English: [requirements.en.md](requirements.en.md)

## 必要環境

- Unity 2022.3
- VRChat SDK Avatars 3.10.6-beta.2 以降（プレリリース。Avatar Optimizerと併用するなら 3.10.6-beta.3 以降）
- NDMF 1.6.0 以降
- Modular Avatar 1.12.0 以降
- Kx VRC ARKit BlendShape Generator 0.2.1-test3 以降（プレリリース）

## プレリリースのSDKを入れる

ネイティブFace Trackingに対応したSDKは、まだプレリリースとして配布されています。VCC/ALCOMの既定ではプレリリースが一覧に出ないので、表示を有効にしてから入れます。

1. VCCなら Settings > Packages の「Show Pre-Release Packages」を有効にする。ALCOMでも設定でプレリリースのパッケージの表示を有効にする
2. プロジェクトの管理画面で「VRChat SDK - Avatars」の版を 3.10.6-beta.2 以降（Avatar Optimizerと併用するなら 3.10.6-beta.3 以降）に変える
3. 本製品を追加する。本製品はこのSDKを依存に持つので、SDKが古いままだと追加できない

本製品が依存に持つKx VRC ARKit BlendShape Generatorも、対応する版はまだプレリリースです。
プレリリースの表示を有効にしてあれば、本製品を追加したときにVCC/ALCOMが一緒に入れます。

## Kx VRC ARKit BlendShape Generator

Generatorは本製品の必須の依存です。
本製品はGeneratorの型を直接使うので、Generatorが入っていないプロジェクトではコンパイルが通りません。
booth版のzipには、本製品とGeneratorのunitypackageが入っています（[インストールと更新](install.md)）。

Generatorを使うかどうかはアバターごとに決められます。
アバターにGeneratorのコンポーネントが無ければ連携は何もせず、DescriptorのBlendshapes Setをそのまま使います。

インスペクタの「Generator連携」が Auto のとき、アバターにGeneratorがあれば、ビルド中だけGeneratorのSet書き込みを有効にします。
右クリックで作ったManagerは、最初からAutoになっています。
GeneratorのSetはビルド中にしか作られないので、インスペクタの見積もりには入りません。

## 入っているときだけ連携するパッケージ

次のパッケージは依存に含めていません。入っているときだけ連携が有効になります。

- Avatar Optimizer 1.9.21-beta.1 以降（併用する場合）

### Avatar Optimizer

Avatar Optimizerは1.9.21-beta.1でネイティブFTに対応し、Blendshapes SetとParameter Behaviorsが使うシェイプを最適化から守るようになりました。
本製品はこの対応を前提にしており、独自のAvatar Optimizer対策は持ちません。
Avatar Optimizerの対応が効くのはVRChat SDK 3.10.6-beta.3以降なので、併用する場合はSDKもこの版以降にしてください。
古い組み合わせでは、ビルド時に警告（KxNFTM-W-14、KxNFTM-W-15）を出します。

古いAvatar Optimizerのまま使う場合は、Trace and Optimizeの「Debug Options」にある「Exclusions」へ顔のメッシュを入れると、そのメッシュのシェイプが削除・統合されなくなります。
そのメッシュは最適化されなくなるので、Avatar Optimizerを更新するまでの回避策として使ってください。
