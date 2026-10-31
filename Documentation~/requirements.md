# 必要環境と併用するパッケージ

English: [requirements.en.md](requirements.en.md)

## 必要環境

- Unity 2022.3
- VRChat SDK Avatars 3.10.6 以降
- NDMF 1.6.0 以降
- Modular Avatar 1.12.0 以降
- Kx VRC ARKit BlendShape Generator 0.2.1 以降

## SDKの版

ネイティブFace TrackingはVRChat SDK 3.10.6で正式版になりました。SDKは安定版の3.10.6以降を入れてください。3.10.6のベータ版（3.10.6-beta.N）は3.10.6より前の版として扱われ、本製品を追加できません。

本製品が依存に持つKx VRC ARKit BlendShape Generatorも安定版の0.2.1以降が対応しているので、プレリリースの表示を有効にする必要はありません。本製品を追加すると、VCC/ALCOMがGeneratorも一緒に入れます。

## Kx VRC ARKit BlendShape Generator

Generatorは本製品の必須の依存です。
本製品はGeneratorの型を直接使うので、Generatorが入っていないプロジェクトではコンパイルが通りません。
booth版のzipには、本製品とGeneratorのunitypackageが入っています（[インストールと更新](install.md)）。

Generatorを使うかどうかはアバターごとに決められます。
アバターにGeneratorのコンポーネントが無ければ連携は何もせず、DescriptorのBlendshapes Setをそのまま使います。

インスペクタの「Generatorコンポーネントを使う」がオンのとき、アバターにGeneratorのコンポーネントがあれば、ビルド中だけGeneratorのSet書き込みを有効にします。
既定はオンで、右クリックで作ったManagerも最初からオンになっています。
GeneratorのSetはビルド中にしか作られないので、インスペクタの見積もりには入りません。

## 入っているときだけ連携するパッケージ

次のパッケージは依存に含めていません。入っているときだけ連携が有効になります。

- Avatar Optimizer 1.9.21 以降（併用する場合）

### Avatar Optimizer

Avatar Optimizerは1.9.21でネイティブFTに対応し、Blendshapes SetとParameter Behaviorsが使うシェイプを最適化から守るようになりました。
本製品はこの対応を前提にしており、独自のAvatar Optimizer対策は持ちません。
1.9.21より前の版（1.9.21のプレリリースを含む）や、Avatar Optimizerの対応が効かない古いSDK（3.10.6-beta.4より前）との組み合わせでは、ビルド時に警告（KxNFTM-W-14、KxNFTM-W-15）を出します。

古いAvatar Optimizerのまま使う場合は、Trace and Optimizeの「Debug Options」にある「Exclusions」へ顔のメッシュを入れると、そのメッシュのシェイプが削除・統合されなくなります。
そのメッシュは最適化されなくなるので、Avatar Optimizerを更新するまでの回避策として使ってください。
