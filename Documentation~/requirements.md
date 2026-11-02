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
booth版のzipには、本製品とGeneratorのunitypackageが入っています（[インストールと更新](install.md)）。

booth版はVCC/ALCOMの依存解決を通らないので、Generatorのunitypackageを取り込み忘れることがあります。
本製品はGeneratorの型をリフレクションで引くので、Generatorが入っていないプロジェクトでもコンパイルが通り、動きます。
そのときは警告（KxNFTM-W-20）を出し、インスペクタの「シェイプキーを自動でセット」と「ARKitをUEに変換」をグレーアウトして、DescriptorにあるBlendshapes Setだけを分割します。
状態表示のGeneratorの欄は「未導入」になります。
Generatorを取り込むと、設定はそのままで連携が有効になります。
連携に必要なフィールドを持たない古い版のGeneratorも、入っていないものとして扱います。

アバターにGeneratorのコンポーネントが無くても、プロジェクトにGenerator 0.2.3以降が入っていれば、その対応表を使います。
DescriptorにBlendshapes Setが1つも無いとき、Face Tracking Meshes（空ならリップシンクのメッシュ）にあるARKit名・UE名のシェイプをキーへ割り当てたSetをビルド中に作り、分割します。
元からARKitのシェイプを持つアバターで、オートフィルを押す手間を省くためです。
Setが1つでもあるとき、アバターにGeneratorのコンポーネントがあるとき、インスペクタの「シェイプキーを自動でセット」がオフのときは作りません。

インスペクタの「ARKitをUEに変換」がオン（既定）なら、Setを作る前に、同じメッシュのARKit名のシェイプキーからUnified Expressions名のシェイプキーをビルド用の複製に書き足します。
変換はGeneratorの出力名をUEにしたときと同じで、browInnerUpやcheekPuffのようにUEで左右・上下に分かれるキーには、分けた形がそれぞれ入ります。
変換を公開している版のGeneratorが要ります。古い版では変換せず、ARKit名のシェイプをそのまま割り当てます。

Generatorを使うかどうかはアバターごとに決められます。
アバターにGeneratorのコンポーネントが無ければ連携は何もせず、DescriptorのBlendshapes Setをそのまま使います。

インスペクタの「シェイプキーを自動でセット」がオンのとき、アバターにGeneratorのコンポーネントがあれば、ビルド中だけGeneratorのSet書き込みを有効にします。
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
