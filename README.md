# Kx VRC NativeFT Manager

VRChatのネイティブFace Tracking向けに、Jerry's Templates相当のトグルと表情補正をEXメニューから操作できるようにするNDMFプラグインです。
ヒエラルキーの右クリックからアバターへ追加するだけで使えます。[Kx VRC ARKit BlendShape Generator](https://github.com/limit7412/VRCARKitBlendShapeGenerator)を依存に持ち、アバターにGeneratorがあれば連携します。
FaceEmoの抑制も本製品が行います。[Kx VRC FE-JsT Bridge](https://github.com/limit7412/VRCFE-JsTBridge)とは連携せず、同じアバターでの併用もしません。

English: [README.en.md](README.en.md)

> [!WARNING]
> **実験的な版（0.x）です。** ネイティブFace TrackingはVRChat SDK 3.10.6で正式版になりましたが、本製品はVRChatのクライアントでの確認が済んで1.0を出すまでは実験的な扱いとします。
> パラメーターの名前（`KxNFTM/` で始まるもの）は1.0までに変わることがあり、変わった場合は保存したトグルの値が既定値へ戻ります。
> VRChatのクライアントで確かめている途中の事項は[実験的な版の制約](Documentation~/limitations.md)にまとめています。

## 必要環境

- Unity 2022.3
- VRChat SDK Avatars 3.10.6 以降
- NDMF 1.6.0 以降
- Modular Avatar 1.12.0 以降
- Kx VRC ARKit BlendShape Generator 0.2.1-test3 以降（プレリリース。VCC/ALCOMでは本製品と一緒に入る）

プレリリースのGeneratorの入れ方と、Avatar Optimizer（1.9.21以降）との併用の条件は[必要環境と併用するパッケージ](Documentation~/requirements.md)にあります。

## インストール

1. [VPMリポジトリ](https://limit7412.github.io/vcc-vpm/)をVCC/ALCOMへ追加する
2. プロジェクトへ「Kx VRC NativeFT Manager」を追加する

zipからの手動インストールとbooth版（unitypackage）の手順、および更新の通知と自己更新の仕組みは[インストールと更新](Documentation~/install.md)にまとめています。

## 使い方

1. ヒエラルキーでアバターを右クリックし、「Kx VRC NativeFT Manager」の `Kx NativeFT Manager` を選ぶ。アバター直下に、コンポーネントを付けたオブジェクトができ、その子にEXメニューの「フェイストラッキング」（MA Menu InstallerとMA Menu Itemの階層）ができる。既存のオブジェクト（アバタールートを含む）へ「Add Component」でコンポーネントを付けてもよいが、その場合のメニューはビルド時に作られる（下の「EXメニュー」）
   - アバターにKx VRC ARKit BlendShape Generatorがあれば、プリセットはARKitになる。GeneratorのSet書き込みはビルド中だけ有効になる
   - Generatorが無ければ、プリセットはUEになる。DescriptorのBlendshapes Setは、オートフィルか手入力で用意しておく
2. インスペクタでプリセットを選び、必要なら下の「設定項目」に沿って個別に変える
3. アップロードする。EXメニューの「フェイストラッキング」から、フェイストラッキング全体・目・口・瞳孔・ビゼームを切り替える。表情補正の切り替え（エミュレーション・制限・両目の連動）は、その下の「設定」にある。表示名は右クリックで作ったときのエディタの言語で付き、ヒエラルキーで自由に変えられる

Jerry's Face Tracking Templatesは同じアバターに置けません。残っているとインスペクタとビルドでエラー（KxNFTM-E-04）が出るので、Jerry'sのプレハブを外してからアップロードしてください。
Jerry'sとの違いは[Jerry'sからの移行](Documentation~/jerrys.md)に、ビルドでアバターに加わるものは[ビルド時に行うこと](Documentation~/build.md)にあります。

## 設定項目

インスペクタの上から順に並べています。インスペクタの表示言語は、先頭の言語切り替えで日本語と英語を選べます。

### 状態と警告

インスペクタの先頭には、DescriptorのBlendshapes Setの数と割り当てられたキーの数、Use All Tracking Keysの状態、Kx VRC ARKit BlendShape Generatorの有無とSet書き込みの状態が出ます。
その下に、設定やアバターの構成に問題があるときのエラー・警告・案内が `KxNFTM-` で始まる番号付きで出ます。原因と対処はそれぞれのメッセージに書いてあり、同じものがビルド時にもNDMFのコンソールへ出ます。

### プリセットとGeneratorコンポーネント

| 項目 | 既定 | 意味 |
| --- | --- | --- |
| プリセット | 右クリックで作るとき、アバターにGeneratorがあれば ARKit、無ければ UE。Add Componentでは UE | トグルの構成と表情補正を作るかの組。選び直すとトグルと表情補正の設定を書き換える。ヒエラルキーのMenu Itemのときは、プリセットに無いトグルのMenu Itemを消し、足りないMenu Itemを足し、既定値をプリセットの値にする（今あるMenu Itemの名前・アイコン・場所はそのまま。Undoで戻せる）。トグルや表情補正をプリセットから変えると Custom になる |
| Generatorコンポーネントを使う | オン | アバターにあるKx VRC ARKit BlendShape Generatorのコンポーネントの、Set書き込みをビルド中だけ有効にする。シーンのGeneratorの設定は変わらない。アバターにGeneratorのコンポーネントが無ければ何もしない。0.1.0-test10までの「Generator連携」の Auto はオン、Off はオフとして読む |

プリセットの中身は次のとおりです。

| プリセット | メニューに出すトグル | 表情補正 | 想定するアバター |
| --- | --- | --- | --- |
| UE | 8つすべて（Eye Syncの既定値はオフ） | エミュレーション・制限・Eye Syncを作る | UE（Unified Expressions）のシェイプを持つ。Jerry'sのUE版に相当 |
| ARKit | Pupil以外の7つ（Eye Syncの既定値はオフ） | UEと同じ | Kx VRC ARKit BlendShape Generatorを使う。ARKitのシェイプには瞳孔が無い |
| Minimal | Face Tracking・Eye Tracking・Lip Trackingの3つ | 作らない | 同期bitとメニューを最小にしたい |
| Custom | 個別に決めた構成 | 個別に決めた構成 | 上のどれかを変えると自動でこれになる |

### EXメニュー

EXメニューの作り方は2通りあり、右クリックで作ったコンポーネントは「ヒエラルキーのMenu Item」になります。

| 作り方 | なるとき | トグルの構成を決める場所 |
| --- | --- | --- |
| ヒエラルキーのMenu Item | 右クリックで作ったとき、または「メニューをヒエラルキーに展開」を押したとき | コンポーネントの子にあるMA Menu Item |
| ビルド時に作る | Add Componentで付けたとき、0.1.0-test8までに作ったコンポーネント | コンポーネントに保存された値（Add Componentの直後はプリセットの値）。インスペクタでは変えられないので、変えるときは展開ボタンでMenu Itemにする |

ヒエラルキーのMenu Itemでは、次の形の階層ができます。Jerry'sと同じ感覚で、並べ替え、表示名とアイコンの変更、別のサブメニューやアバターの別の場所への移動ができます。

```
フェイストラッキング          MA Menu Installer ＋ Menu Item（サブメニュー）
├ フェイストラッキング        トグル  KxNFTM/FaceTracking
├ 目のトラッキング            トグル  KxNFTM/EyeTracking
├ 口のトラッキング            トグル  KxNFTM/LipTracking
├ 瞳孔                        トグル  KxNFTM/Pupil（UEプリセットのみ）
├ ビゼーム                    トグル  KxNFTM/Visemes
├ 感度                        サブメニュー（感度のRadialを足したときだけ）
│ ├ 目                        Radial  KxNFTM/Sensitivity/Eye
│ └ 口                        Radial  KxNFTM/Sensitivity/Lip
└ 設定                        サブメニュー
  ├ エミュレーション          トグル  KxNFTM/Emulation
  ├ 制限                      トグル  KxNFTM/Limits
  └ 両目の連動                トグル  KxNFTM/EyeSync
```

ビルド時には、アバターにあるMA Menu Itemのうち、パラメーター名（MA Parametersのリネームを当てた後の名前）が `KxNFTM/` の公開名と一致するものを読みます。

- Menu Itemがあるトグルはメニューに出し、同期Boolのパラメーター（1つにつき1bit）を作る。既定値はMenu Itemの既定値（Default）、保存はMenu Itemの保存（Saved）の設定に従う。同期は設定にかかわらず常に有効にする（リモートのクライアントも自分のアニメーターでSetの重みを決めるため）
- Menu Itemを消したトグルはメニューに出ず、パラメーターも作らない。コンポーネントに保存された既定値のまま動く（右クリックで作った直後とプリセットを選び直した後はプリセットの値。展開ボタンで移行したときは、移行前の既定値を引き継ぐ）
- トグルのMenu Itemの値は1にする（値の自動設定でもよい）。1以外の値のMenu Itemと、同じパラメーターの項目が複数あるのに値の自動設定を使っているMenu Itemは読まず、警告（KxNFTM-W-19）を出す
- 公開名を指すMenu Itemが1つも無いと、警告（KxNFTM-W-18）を出す
- パラメーター名が公開名と違うMenu Itemは、利用者のものとして扱い読まない

ビルド時に作る形のコンポーネントには、インスペクタの「トグル」に「メニューをヒエラルキーに展開」ボタンが出ます。押すと、そのときのトグルと感度の設定のとおりにMenu Itemを作り、以後はMenu Itemの側で編集する形に変わります（Undoで戻せます）。

### トグル

EXメニューに出すトグルは、ヒエラルキーのMenu Itemで決めます。インスペクタにトグルの一覧表はありません。Menu Itemを消したトグルを戻すときは、プリセットを選び直すか、Menu Itemを複製してパラメーター名を公開名にします。

| トグル | メニューでの切り替え対象 |
| --- | --- |
| Face Tracking | フェイストラッキング全体。切るとすべてのSet（首の NeckFlex を含む）の重みが0になり、FaceEmoの抑制も解除される |
| Eye Tracking | 目・まぶた・瞳孔・眉のSet |
| Lip Tracking | 口のSet（頬、舌を含む） |
| Pupil | 瞳孔のSet。Eye Trackingを切っているときは、こちらを入れても動かない |
| Visemes | ビゼーム（音声による口の動き）。切ると、FXのTracking ControlでMouth & JawをAnimationにしてビゼームを止める |
| Emulation | 表情補正のエミュレーション。EXメニューでは「Settings」の中に出る |
| Limits | 表情補正の制限。「Settings」の中に出る |
| Eye Sync | 左右のまぶたを揃える補正。「Settings」の中に出る |

表情補正のEmulation・Limits・Eye Syncは、次の「表情補正」で対応する補正を作らない設定にしているあいだ、Menu Itemが残っていてもビルド時にメニューから外します（KxNFTM-I-05）。インスペクタの「トグル」にもその旨の案内が出ます。

### 表情補正

Jerry's 7.0.5のFXにある補正を、DescriptorのParameter Behaviors（Custom式）として作ります。どれもオフなら、その補正もメニューのトグルも作りません。

| 項目 | UE・ARKitの既定 | 意味 |
| --- | --- | --- |
| エミュレーション | 有効 | 笑うと頬とえくぼが上がる、口の動きで眉が動く、見開くと瞳孔が縮む、などを足す。トラッキングの値を下回らない下限として書く |
| 制限 | 有効 | 唇をすぼめたときに上唇を上げすぎない、など同時に動かすと崩れる組み合わせに上限を掛ける |
| Eye Sync | 有効（メニューの既定値はオフ） | 左右のまばたきを平均して両目へ書く |
| 規則の一覧 | すべてチェック | 補正を規則ごとに表示する折りたたみ。チェックを外した規則は作らない。外すとプリセットは Custom になる。対象のシェイプがSetに無い規則には「対象のシェイプなし」と出て、ビルドでも作られない |

アバター全体のCustom式の命令数が上限の512を超えるときは、優先度の低い規則から作らずに警告（KxNFTM-W-12）を出します。

### 感度

EXメニューの「Face Tracking」に「Sensitivity」を足し、Radialで目と口の効き方を変えられるようにします。プリセットには含まれないので、変えてもCustomにはなりません。
ヒエラルキーのMenu Itemのときは、下の目・口の感度の代わりに「Radialを追加」「Radialを削除」のボタンが出て、RadialのMenu Itemの有無で系統が決まります。値の保存はRadialのMenu Itemの保存（Saved）に従います。

| 項目 | 既定 | 意味 |
| --- | --- | --- |
| 目の感度 | 無効 | 目・まぶた・瞳孔・眉のSetに掛かるRadialを足す。同期パラメーター `KxNFTM/Sensitivity/Eye`（Float、8bit）を作る |
| 口の感度 | 無効 | 口・頬・舌・ビゼームのSetに掛かるRadialを足す。同期パラメーター `KxNFTM/Sensitivity/Lip`（Float、8bit）を作る |
| 上限の倍率 | 2 | Radialを右端まで回したときの倍率（1〜4）。目か口の感度を有効にしたときだけ出る |
| 値を保存する | 有効 | ワールドをまたいでRadialの値を保存する。目か口の感度を有効にしたときだけ出る |

Radialの中央（既定値）が等倍で、8段階で切り替わります。左側は0.2〜0.8倍、右側は等倍から上限の倍率までです。
首（NeckFlex）と表情補正の効き方は変わりません。トグルでSetを止めたときは、感度にかかわらず止まります。
段階的にしか変わらない理由は[ビルド時に行うこと](Documentation~/build.md#感度の仕組み)にあります。

### Descriptorの設定（ビルド時）

ビルド用の複製のDescriptorに書く値です。シーンのアバターは書き換えません。DescriptorのFace Tracking自体は、この設定にかかわらず有効にします。

| 項目 | 既定 | 意味 |
| --- | --- | --- |
| Use All Tracking Keysを有効にする | 有効 | DescriptorのUse All Tracking Keysを有効にする。無効のままだと、目の基本キー10個以外のトラッキングが使われない（KxNFTM-W-05）。オフにするとDescriptorの値を変えない |
| Viseme Dampen | DoNotChange | DescriptorのViseme Dampenのモード。DoNotChange ならDescriptorの値のまま。None・DampenTracking・DampenVisemes はDescriptorの同名のモードをそのまま書く |
| 量 | 0.5 | Viseme Dampenと一緒に書く量（0〜1）。DampenTracking か DampenVisemes のときだけ出る |

### FaceEmoの抑制

FaceEmoを使うアバターで、フェイストラッキング中にFaceEmoの表情とまばたきが顔のシェイプを書き込まないようにします。
FaceEmoがあるのに無効のときは、インスペクタに案内（KxNFTM-I-03）が出ます。下の「FaceEmoを抑制する」以外の項目は、有効にしたときだけ出ます。

| 項目 | 既定 | 意味 |
| --- | --- | --- |
| FaceEmoを抑制する | 無効 | 抑制のレイヤーを作る。無効なら、追加で書き込むパラメーターと取り除くFXレイヤーも使わない |
| 切り替え先の表情番号 | 0 | トラッキング中に切り替えるFaceEmoの表情の番号。FaceEmoの「表情選択」メニューが書く番号と同じで、表情パターンの先頭から0、1、2…と数える |
| 発動条件 | 目か口のトラッキングが有効 | 「目か口のトラッキングが有効」はFace Trackingが有効で、Eye TrackingかLip Trackingのどちらかが有効なとき。「口のトラッキングが有効」はFace TrackingとLip Trackingが有効なときだけで、目だけのトラッキングではFaceEmoのまばたきが残る |
| 追加で書き込むパラメーター | 空 | フェイストラッキングが有効な間だけ値を書き込むパラメーター（MAで作った表情トグルなど）の一覧。各項目は下の表 |
| 取り除くFXレイヤー | 空 | ビルド時にFXから取り除くレイヤーの名前。まばたきやTracking Controlの切り替えなど、トラッキングとぶつかる素体のレイヤーを並べる。素体のアセットは書き換えない。名前はビルド後のFXをAnimatorウィンドウで開くと確認できる |

抑制は、FaceEmoを動かしたまま表情ロック・まばたき停止・表情の切り替えを書き込む形で行います。
FaceEmoが書き込みを続けるので、素体の表情レイヤーは通常時と同じく押さえ込まれ、トラッキング中もメニューから表情を選べます。
ただしFaceEmoのデフォルト表情はトラッキングと同じシェイプを書くため、FaceEmoの「除外するブレンドシェイプ」にトラッキング用のシェイプを登録しておいてください。
発動はメニューのトグルだけで決まり、トラッカーが値を送っているかは見ません。トラッカーを使わないときは、EXメニューのFace Trackingを切ってください。

追加で書き込むパラメーターの各項目は次のとおりです。

| 項目 | 既定 | 意味 |
| --- | --- | --- |
| 指定方法 | MA Menu Item | 書き込む先を、MA Menu Itemで指定するかパラメーター名で指定するか |
| メニューアイテム | なし | 指定方法が MA Menu Item のとき。トグルかボタンのMA Menu Itemを指定し、パラメーター名と値はビルド時に読み取る |
| トラッキング中の状態 | OFF | 指定方法が MA Menu Item のとき。トラッキング中にメニューアイテムをONとOFFのどちらにするか |
| パラメーター名 | 空 | 指定方法がパラメーター名のとき。Modular Avatarがリネームしたあとの、FXで使われる名前を書く |
| 型 | Bool | 指定方法がパラメーター名のとき。Bool・Int・Float |
| トラッキング中の値 | 0 | 指定方法がパラメーター名のとき。トラッキング中に書く値 |
| 解除時 | 元の値に戻す | フェイストラッキングを切ったときに、元の値に戻す・解除時の値を書く・何もしない、のどれにするか |
| 解除時の値 | 1 | 解除時が「解除時の値を書く」のときに書く値。MA Menu Itemでは、トラッキング中と反対の状態へ切り替える |
| 同期 | 自動 | 同期パラメーターは装着者のクライアントだけで、切り替わった瞬間に一度だけ書く。同期しないパラメーターは各クライアントで書き、トラッキング中は約1秒ごとに書き直す。自動はExpression Parameters・MA Parameters・MA Menu Itemの設定から判定する |

### 見積もり（ビルド前の目安）

Descriptorの今の値から、ビルドで作るものの量を計算して表示します。設定する項目はありません。

| 表示 | 意味 |
| --- | --- |
| 分割後のSet | グループに分けたあとのSetの数と、表情補正の切り替えに使う空のSet（ゲート）の数 |
| Parameter Behaviors | 本製品が作る数と、既存の数 |
| Custom式の命令数 | アバター全体の命令数と上限512、そのうち本製品の分 |
| Face Tracking Operations | SetとParameter Behaviors（既存を含む）から数えた操作の数。VRChatの数え方は細部が公開されていないので目安 |
| 同期bit | アバター全体の同期bitと上限、そのうち本製品の分。超える見込みなら警告（KxNFTM-W-13） |
| FXレイヤー | 本製品が作るFXのレイヤーの数 |

GeneratorのSetを使う場合、そのSetはビルド中にしか無いので、見積もりに入りません。

### 詳細

折りたたみの中にある、普段は変えなくてよい項目です。

| 項目 | 既定 | 意味 |
| --- | --- | --- |
| Lerp Speed | 8 | トグルを切り替えたときに、Setの重みが追いつく速さ（0〜20）。0で即時に切り替わる |
| 分割しないSet | 空 | ここに名前を入れたBlendshapes Setは、グループに分けずにそのまま残す。そのSetはトグルでは切り替わらず、表情補正の対象にもならない |

## ドキュメント

- [必要環境と併用するパッケージ](Documentation~/requirements.md)
- [インストールと更新](Documentation~/install.md)
- [ビルド時に行うこと](Documentation~/build.md)
- [Jerry'sからの移行](Documentation~/jerrys.md)
- [実験的な版の制約](Documentation~/limitations.md)

開発者向けの情報（開発環境とテスト、CI、配布パッケージ）は[開発環境とテストの実行](docs/development.md)から辿れます。
仕様と全体計画は [issue #1](https://github.com/limit7412/VRCNativeFTManager/issues/1) にまとめています。

## ライセンス

[MIT License](LICENSE)

表情補正の係数はJerry's Face Tracking Templates（MIT License、Copyright (c) 2023 Adjerry91）から移しています。許諾表示は[THIRD_PARTY_NOTICES](Editor/THIRD_PARTY_NOTICES.md)にあります。
