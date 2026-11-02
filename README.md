# Kx VRC NativeFT Manager

VRChatのネイティブFace Tracking向けに、Jerry's Templates相当のトグルと表情補正をEXメニューから操作できるようにするNDMFプラグインです。
ヒエラルキーの右クリックからアバターへ追加するだけで使えます。[Kx VRC ARKit BlendShape Generator](https://github.com/limit7412/VRCARKitBlendShapeGenerator)を依存に持ち、アバターにGeneratorがあれば連携します。
FaceEmoの抑制も本製品が行います。[Kx VRC FE-JsT Bridge](https://github.com/limit7412/VRCFE-JsTBridge)とは連携せず、同じアバターでの併用もしません。

English: [README.en.md](README.en.md)

## 必要環境

- Unity 2022.3
- VRChat SDK Avatars 3.10.6 以降
- NDMF 1.6.0 以降
- Modular Avatar 1.12.0 以降
- Kx VRC ARKit BlendShape Generator 0.2.3 以降（VCC/ALCOMでは本製品と一緒に入る。booth版で取り込み忘れても本製品は動き、Generatorとの連携だけが止まる）

Generatorとの関係と、Avatar Optimizer（1.9.21以降）との併用の条件は[必要環境と併用するパッケージ](Documentation~/requirements.md)にあります。

## インストール

1. [VPMリポジトリ](https://limit7412.github.io/vcc-vpm/)をVCC/ALCOMへ追加する
2. プロジェクトへ「Kx VRC NativeFT Manager」を追加する

zipからの手動インストールとbooth版（unitypackage）の手順、および更新の通知と自己更新の仕組みは[インストールと更新](Documentation~/install.md)にまとめています。

## 使い方

1. ヒエラルキーでアバターを右クリックし、「Kx VRC NativeFT Manager」の `Kx NativeFT Manager` を選ぶ。アバター直下にコンポーネントを付けたオブジェクトができ、その子にEXメニューの「Face Tracking」（MA Menu InstallerとMA Menu Itemの階層）ができる
2. 必要なら、インスペクタとヒエラルキーのMenu Itemで設定を変える。項目の意味は[設定項目](Documentation~/settings.md)にある
3. アップロードする。EXメニューの「Face Tracking」から、フェイストラッキング全体（Face Tracking）・目（Eye Tracking）・口（Lip Tracking）・瞳孔（Pupil）・ビゼーム（Visemes）を切り替える。表情補正の切り替え（Emulation・Limits・Eye Sync）は、その下の「Settings」にある

追加した直後は、トグルをすべてメニューに出し、表情補正（エミュレーション・制限・Eye Sync）をすべて作る設定になっています。
DescriptorのBlendshapes Setは、アバターにKx VRC ARKit BlendShape Generatorがあればビルド中にGeneratorが書きます。無ければDescriptorのSetをそのまま使い、Setが1つも無いときは顔のメッシュのARKit名・UE名のシェイプからビルド中に作ります。元からARKitのシェイプを持つアバターなら、オートフィルを押さなくてかまいません。
既存のオブジェクト（アバタールートを含む）へ「Add Component」で付けることもできますが、その場合のEXメニューはビルド時に作られ、ヒエラルキーでは編集できません（[EXメニュー](Documentation~/settings.md#exメニュー)）。

Jerry's Face Tracking Templatesは同じアバターに置けません。残っているとインスペクタとビルドでエラー（KxNFTM-E-04）が出るので、Jerry'sのプレハブを外してからアップロードしてください。
Jerry'sとの違いは[Jerry'sからの移行](Documentation~/jerrys.md)に、ビルドでアバターに加わるものは[ビルド時に行うこと](Documentation~/build.md)にあります。

## ドキュメント

- [必要環境と併用するパッケージ](Documentation~/requirements.md)
- [インストールと更新](Documentation~/install.md)
- [設定項目](Documentation~/settings.md)
- [ビルド時に行うこと](Documentation~/build.md)
- [Jerry'sからの移行](Documentation~/jerrys.md)
- [制約と注意点](Documentation~/limitations.md)

開発者向けの情報（開発環境とテスト、CI、配布パッケージ）は[開発環境とテストの実行](docs/development.md)から辿れます。
仕様と全体計画は [issue #1](https://github.com/limit7412/VRCNativeFTManager/issues/1) にまとめています。

## ライセンス

[MIT License](LICENSE)

表情補正の係数はJerry's Face Tracking Templates（MIT License、Copyright (c) 2023 Adjerry91）から移しています。許諾表示は[THIRD_PARTY_NOTICES](Editor/THIRD_PARTY_NOTICES.md)にあります。
