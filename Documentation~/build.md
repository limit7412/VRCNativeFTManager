# ビルド時に行うこと

English: [build.en.md](build.en.md)

本製品はNDMFのビルド中に、ビルド用の複製へ次のものを加えます。シーンのアバターとアセットは書き換えません。
それぞれを有効にする設定は、READMEの[設定項目](../README.md#設定項目)にあります。

- DescriptorのBlendshapes Setを、トラッキングキーの種類でEye / EyeClosed / Pupil / Brow / Lip / Otherに分ける（`KxNFTM/Eye` など）。元のSetは消え、キーとシェイプの対応の総数は変わらない。「分割しないSet」に入れたSetと、既存のParameter BehaviorかState Behaviorが名前で参照しているSet（KxNFTM-W-10）は分けずに残す
- メニューに出すトグルごとに同期Boolのパラメーターを作り（ビルド時に作る形ならメニューも作り、ヒエラルキーのMenu Itemの形ならMenu Itemから既定値と保存を読み）、FXのレイヤーでSetの重みを0と1に切り替える。パラメーターは `KxNFTM/FaceTracking`、`KxNFTM/EyeTracking`、`KxNFTM/LipTracking`、`KxNFTM/Pupil`、`KxNFTM/Visemes`、`KxNFTM/Emulation`、`KxNFTM/Limits`、`KxNFTM/EyeSync` で、UEプリセットなら計8bit
- 切り替えるSetが無いトグル（ARKitのシェイプしか持たないアバターのPupilなど）は、EXメニューの項目と同期パラメーターを外す（KxNFTM-I-05）。生成しない表情補正のトグルのMenu Itemがヒエラルキーに残っているときも同じく外す
- 感度を有効にした系統は、Setの倍率を上限の倍率まで上げ、Radialの段階ごとのステートでSetの重みを書く（下の「感度の仕組み」）
- Visemesを切ると、FXのTracking ControlでMouth & JawをAnimationにしてビゼームを止める
- 表情補正を、DescriptorのParameter Behaviors（Custom式）として足す。係数はJerry's 7.0.5のFXから移した（出典は[THIRD_PARTY_NOTICES](../Editor/THIRD_PARTY_NOTICES.md)）
  - エミュレーション: 笑うと頬と口角のくぼみを上げる、口から眉を動かす、見開くと瞳孔を縮めるなど。追跡した値を下回らない下限（Limit Min）として書く
  - 制限: 唇をすぼめると口の上下を抑えるなど、同時に動かすと崩れる組み合わせの上限（Limit Max）を書く
  - Eye Sync: 左右のまばたきを平均して両目へ書く
  - 補正の切り替えには、対応を持たない空のSet（`KxNFTM/Gate/...`）の重みを使う。規則が読み書きするキーの側のトグル（目・瞳孔・口）を切ったときも、その規則は止まる
  - 対象のシェイプがSetに無い規則は作らない（KxNFTM-I-01）。Custom式の命令数がアバター全体の上限512を超えるときは、優先度の低い規則から落とす（KxNFTM-W-12）
- FaceEmoの抑制を有効にしたときは、FaceEmoと追加で書き込むパラメーターを制御するFXレイヤーを作り、「取り除くFXレイヤー」に挙げたレイヤーをFXから取り除く
- DescriptorのFace Trackingを有効にする。Use All Tracking KeysとViseme Dampenは、インスペクタの「Descriptorの設定」に従って書く

## 感度の仕組み

Jerry'sの感度は7系統の連続値でしたが、ネイティブFTのParameter Behaviorsはアニメーターのパラメーターを読めないため、本製品はSetの重みで近似しています。
ビルド時に対象のSetの倍率を上限の倍率まで上げておき、Radialの段階ごとにSetの重みを割り戻して書きます。
そのため段階の間は連続して変わらず、倍率は元のSetの倍率にだけ掛かります。
感度を変えても、表情補正の効き方は変わりません。
