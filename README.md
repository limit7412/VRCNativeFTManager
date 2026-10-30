# Kx VRC NativeFT Manager

VRChatのネイティブFace Tracking向けに、Jerry's Templates相当のトグルと表情補正をEXメニューから操作できるようにするNDMFプラグインです。
ヒエラルキーの右クリックからアバターへ追加するだけで使え、[Kx VRC ARKit BlendShape Generator](https://github.com/limit7412/VRCARKitBlendShapeGenerator)に連携します。
FaceEmoの抑制も本製品が行います。[Kx VRC FE-JsT Bridge](https://github.com/limit7412/VRCFE-JsTBridge)とは連携せず、同じアバターでの併用もしません。

English: [README.en.md](README.en.md)

> [!WARNING]
> **実験的な版（0.x）です。** ネイティブFace TrackingはVRChat SDKのベータ版の機能で、SDKが正式版になるまでは仕様が変わる可能性があります。本製品も、SDKの正式版に合わせて1.0を出すまでは実験的な扱いとします。
> 現在のバージョンで動くのは、Blendshapes Setのグループ分割、FT全体・目・口・瞳孔・ビゼームのトグル、表情補正（エミュレーション、制限、Eye Sync）、感度、FaceEmoの抑制です。
> トグルが使うState Behaviorの型と、表情補正の切り替えに使う空のSet（ゲート）が効くかは、VRChatのベータクライアントで確かめている途中です。State Behaviorの型が見つからないときはビルドで警告（KxNFTM-E-05）が出て、トグルと感度だけが効きません。
> パラメーターの名前（`KxNFTM/` で始まるもの）は1.0までに変わることがあります。変わった場合は、保存したトグルの値が既定値へ戻ります。

## 必要環境

- Unity 2022.3
- VRChat SDK Avatars 3.10.6-beta.2 以降（プレリリース。下の「プレリリースのSDKを入れる」を参照）
- NDMF 1.6.0 以降
- Modular Avatar 1.12.0 以降

### プレリリースのSDKを入れる

ネイティブFace Trackingに対応したSDKは、まだプレリリースとして配布されています。VCC/ALCOMの既定ではプレリリースが一覧に出ないので、表示を有効にしてから入れます。

1. VCCなら Settings > Packages の「Show Pre-Release Packages」を有効にする。ALCOMでも設定でプレリリースのパッケージの表示を有効にする
2. プロジェクトの管理画面で「VRChat SDK - Avatars」の版を 3.10.6-beta.2 以降（Avatar Optimizerと併用するなら 3.10.6-beta.3 以降）に変える
3. 本製品を追加する。本製品はこのSDKを依存に持つので、SDKが古いままだと追加できない

次のパッケージは依存に含めていません。入っているときだけ連携が有効になります。

- Kx VRC ARKit BlendShape Generator
- Avatar Optimizer 1.9.21-beta.1 以降（併用する場合）

Avatar Optimizerは1.9.21-beta.1でネイティブFTに対応し、Blendshapes SetとParameter Behaviorsが使うシェイプを最適化から守るようになりました。
本製品はこの対応を前提にしており、独自のAvatar Optimizer対策は持ちません。
Avatar Optimizerの対応が効くのはVRChat SDK 3.10.6-beta.3以降なので、併用する場合はSDKもこの版以降にしてください。
古い組み合わせでは、ビルド時に警告（W-14、W-15）を出します。

古いAvatar Optimizerのまま使う場合は、Trace and Optimizeの「Debug Options」にある「Exclusions」へ顔のメッシュを入れると、そのメッシュのシェイプが削除・統合されなくなります。
そのメッシュは最適化されなくなるので、Avatar Optimizerを更新するまでの回避策として使ってください。

## インストール

1. [VPMリポジトリ](https://limit7412.github.io/vcc-vpm/)をVCC/ALCOMへ追加する
2. プロジェクトへ「Kx VRC NativeFT Manager」を追加する

zipからの手動インストールとbooth版（unitypackage）の手順、および更新の通知と自己更新の仕組みは[インストールと更新](Documentation~/install.md)にまとめています。

## 使い方

1. ヒエラルキーでアバターを右クリックし、「Kx VRC NativeFT Manager」から次のどちらかを選ぶ。アバター直下に、コンポーネントを付けたオブジェクトができる。既存のオブジェクト（アバタールートを含む）へ「Add Component」でコンポーネントを付けても同じように動く
   - 汎用版（`Kx NativeFT Manager`）: DescriptorのBlendshapes Setを、オートフィルか手入力で用意してあるアバター向け
   - ARKit版（`Kx NativeFT Manager (ARKit)`）: Kx VRC ARKit BlendShape Generatorを使うアバター向け。GeneratorのSet書き込みを、ビルド中だけ有効にする
2. インスペクタでプリセットを選ぶ。UE（既定）とARKitはJerry'sと同じトグルと表情補正を持ち、Minimalは表情補正を作らずトグルも3つだけにする。トグルや補正の規則を個別に変えるとCustomになる
3. アップロードする。EXメニューの「Face Tracking」から、Face Tracking・Eye Tracking・Lip Tracking・Pupil・Visemesを切り替える。表情補正の切り替え（Emulation・Limits・Eye Sync）は、その下の「Settings」にある

Jerry's Face Tracking Templatesは同じアバターに置けません。残っているとインスペクタとビルドでエラー（KxNFTM-E-04）が出ます。
本製品はJerry'sを取り除かないので、Jerry'sのプレハブを外してからアップロードしてください。Jerry's版とネイティブ版を使い分けたいときは、アバターを分けてアップロードします。

### ビルド時に行うこと

- DescriptorのBlendshapes Setを、トラッキングキーの種類でEye / EyeClosed / Pupil / Brow / Lip / Otherに分ける（`KxNFTM/Eye` など）。元のSetは消え、キーとシェイプの対応の総数は変わらない
- メニューに出すトグルごとに同期Boolのパラメーターとメニューを作り、FXのレイヤーでSetの重みを0と1に切り替える。パラメーターは `KxNFTM/FaceTracking`、`KxNFTM/EyeTracking`、`KxNFTM/LipTracking`、`KxNFTM/Pupil`、`KxNFTM/Visemes`、`KxNFTM/Emulation`、`KxNFTM/Limits`、`KxNFTM/EyeSync` で、UEプリセットなら計8bit
- 感度を有効にした系統は、Setの倍率を上限の倍率まで上げ、Radialの段階ごとのステートでSetの重みを書く（下の「感度」）
- Visemesを切ると、FXのTracking ControlでMouth & JawをAnimationにしてビゼームを止める
- 表情補正を、DescriptorのParameter Behaviors（Custom式）として足す。係数はJerry's 7.0.5のFXから移した（出典は[THIRD_PARTY_NOTICES](Editor/THIRD_PARTY_NOTICES.md)）
  - エミュレーション: 笑うと頬と口角のくぼみを上げる、口から眉を動かす、見開くと瞳孔を縮めるなど。追跡した値を下回らない下限（Limit Min）として書く
  - 制限: 唇をすぼめると口の上下を抑えるなど、同時に動かすと崩れる組み合わせの上限（Limit Max）を書く
  - Eye Sync: 左右のまばたきを平均して両目へ書く
  - 補正の切り替えには、対応を持たない空のSet（`KxNFTM/Gate/...`）の重みを使う。規則が読み書きするキーの側のトグル（目・瞳孔・口）を切ったときも、その規則は止まる
  - 対象のシェイプがSetに無い規則は作らない（KxNFTM-I-01）。Custom式の命令数がアバター全体の上限512を超えるときは、優先度の低い規則から落とす（KxNFTM-W-12）
- DescriptorのFace Trackingを有効にし、Use All Tracking Keysを有効にする（インスペクタで変更しないこともできる）

### 感度

インスペクタの「感度」で目か口を有効にすると、EXメニューの「Face Tracking」に「Sensitivity」が増え、その中のRadialで目と口の効き方を変えられます。
同期パラメーターは `KxNFTM/Sensitivity/Eye` と `KxNFTM/Sensitivity/Lip`（Float）で、1つにつき同期bitが8増えます。既定ではどちらも作りません。

- 目の感度は、目・まぶた・瞳孔・眉のSetに、口の感度は口のSet（頬、舌、ビゼームを含む）に掛かる。首（NeckFlex）には掛からない
- Radialの中央（既定値）が等倍で、8段階で切り替わる。左側は0.2〜0.8倍、右側は等倍からインスペクタの「上限の倍率」（既定2倍）まで
- 表情補正（エミュレーション・制限・Eye Sync）の効き方は変わらない

Jerry'sの感度は7系統の連続値でしたが、ネイティブFTのParameter Behaviorsはアニメーターのパラメーターを読めないため、Setの重みで近似しています。
ビルド時に対象のSetの倍率を上限の倍率まで上げておき、Radialの段階ごとにSetの重みを割り戻して書きます。
そのため段階の間は連続して変わらず、倍率は元のSetの倍率にだけ掛かります。

### FaceEmoの抑制

FaceEmoを使うアバターでは、インスペクタの「FaceEmoの抑制」を有効にすると、フェイストラッキング中にFaceEmoの表情とまばたきが顔のシェイプを書き込まないようにします。
FE-JsT Bridgeの同じ機能を移したもので、FaceEmoのアセットは読まず、パラメーター名でつなぎます。既定は無効で、FaceEmoがあるのに無効のときはインスペクタに案内（KxNFTM-I-03）が出ます。

- 発動条件: 「目か口のトラッキングが有効」（Face Trackingが有効で、Eye TrackingかLip Trackingが有効）か、「口のトラッキングが有効」を選ぶ。メニューのトグルだけで決まり、トラッカーが値を送っているかは見ないので、トラッカーを使わないときはFace Trackingのトグルを切る
- 止め方: FaceEmoを動かしたまま、表情ロック・まばたき停止・「切り替え先の表情番号」への切り替えを書き込む（表情制御）。FaceEmoは書き込みを続けるので、素体の表情レイヤーは通常時と同じく押さえ込まれ、トラッキング中もメニューから表情を選べる。FaceEmoのデフォルト表情はトラッキングと同じシェイプを書くので、FaceEmoの「除外するブレンドシェイプ」にトラッキング用のシェイプを登録しておく。FE-JsT Bridgeにあった「バイパス」（FaceEmoごと止める方式）は持たない
- 追加で書き込むパラメーター: フェイストラッキング中だけ値を書き込むパラメーター。MA Menu Itemかパラメーター名で指定し、解除時は元の値に戻す・解除時の値を書く・何もしない、から選ぶ
- 取り除くFXレイヤー: ビルド時に、DescriptorのFXから指定した名前のレイヤーを取り除く。素体のアセットは書き換えない

FE-JsT Bridgeにあった「Tracking Controlの再適用」と「FaceEmoとトラッキングのシェイプの重なりの警告」は、ネイティブFTでの必要性をVRChatのベータクライアントで確かめてから入れるかを決めます。

### トラッキングのデータが来ないとき

トラッカーが止まったときや、VRCFaceTrackingを終了したときに、顔のシェイプが0へ戻るか最後の値で止まるかはVRChatが決めます。どちらになるかは、ベータクライアントでまだ確かめていません。
トラッカーを使わない間は、EXメニューの「Face Tracking」を切ってください。Setの重みが0になり、FaceEmoの抑制も解除されます。
同じアバターをトラッカーなしで使う時間が長いなら、トグルの既定値を切っておく方法もあります。

### 見積もり

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
| 感度 | 7系統の連続値 | 目と口の2系統、8段階（既定では作らない） |

パラメーターの接頭辞 `KxNFTM/` は仮の名前で、1.0までに確定します。
メニューの表示名はJerry'sと同じく英語です。

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
